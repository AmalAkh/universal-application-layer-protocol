using System.Diagnostics;
using System.Net;
using Timers = System.Timers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using CustomProtocol.Net.Exceptions;
using NUnit.Framework.Constraints;


namespace CustomProtocol.Net
{

    public class CustomUdpClient
    {
       

        
        private Connection _connection;

        protected Socket _sendingSocket;
        protected Socket _listeningSocket;
        

        
        
        FragmentManager _fragmentManager = new FragmentManager();
        public CustomUdpClient()
        {
           
        }
        
        public void Start(string address, ushort listeningPort, ushort sendingPort)
        {
            _listeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
            _listeningSocket.Bind(new IPEndPoint(IPAddress.Parse(address), listeningPort));
            _listeningSocket.ReceiveBufferSize = (Int16.MaxValue/2)*10;
            _sendingSocket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
            _sendingSocket.Bind(new IPEndPoint(IPAddress.Parse(address), sendingPort));
            _sendingSocket.SendBufferSize = (Int16.MaxValue/2)*10;

            _connection = new Connection(_listeningSocket, _sendingSocket);
            _connection.Interrupted+= ()=>
            {
                ClearBeforeDisconnection();
            };

          
            

            StartListening();
        

            Console.WriteLine($"Listening on address {address} on port {listeningPort}");
            Console.WriteLine($"Sending using address {address} on port {sendingPort}");


            
        }
        
        protected void StartListening()
        {
           
        
            Task task = new Task(async()=>
            {

                while(true)
                {
                    byte[] bytes = new byte[1500];//buffer
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.None,0);
                    
                    SocketReceiveFromResult receiveFromResult = await _listeningSocket.ReceiveFromAsync(bytes, endPoint);
                    var senderEndPoint = receiveFromResult.RemoteEndPoint as IPEndPoint;
                    

                    CustomProtocolMessage incomingMessage = new CustomProtocolMessage();
                    try
                    {
                        incomingMessage = CustomProtocolMessage.FromBytes(bytes.Take(receiveFromResult.ReceivedBytes).ToArray());
                    }catch(DamagedMessageException e)
                    {
                        //Console.WriteLine("Fragment damaged");
                        continue;
                    }
                    if(_connection.IsConnectionTimeout)
                    {   
                        await _connection.InterruptConnectionHandshake();
                    
                    }else if(_connection.IsConnectionInterrupted)
                    {    await _connection.InterruptConnection();

                    }else if(_connection.Status == ConnectionStatus.Unconnected &&  incomingMessage.Syn && !incomingMessage.Ack)
                    {
                        await _connection.AcceptConnection(new IPEndPoint(senderEndPoint.Address, BitConverter.ToInt16(incomingMessage.Data) ));
                    }else if(_connection.Status == ConnectionStatus.WaitingForIncomingConnectionAck && incomingMessage.Ack && !incomingMessage.Syn)
                    {
                                                
                        await _connection.EstablishConnection();
                    }else if(_connection.Status == ConnectionStatus.WaitingForOutgoingConnectionAck && incomingMessage.Ack && incomingMessage.Syn)
                    {
                        await _connection.EstablishOutgoingConnection(new IPEndPoint(senderEndPoint.Address, BitConverter.ToInt16(incomingMessage.Data) ));
                    }else if(_connection.Status == ConnectionStatus.Connected && incomingMessage.Pong)
                    {
                        _connection.ReceivePong();
                    }else if(_connection.Status == ConnectionStatus.Connected && incomingMessage.Finish)
                    {
                        await _connection.AcceptDisconnection();
                    }
                    else if(_connection.Status == ConnectionStatus.Connected && incomingMessage.Ack && !incomingMessage.Syn)
                    {
                        if(_unAcknowledgedMessages.ContainsKey(incomingMessage.Id))
                        {

                            _unAcknowledgedMessages[incomingMessage.Id].RemoveAll((seqNum)=>seqNum==incomingMessage.SequenceNumber);

                        }
                    }else if(incomingMessage.Ping)
                    {
                        await _connection.SendPong();
                    }else if(incomingMessage.Syn && _connection.Status == ConnectionStatus.Connected)
                    {
                        await _connection.SendMessage(_currentFragmentsPortions[incomingMessage.Id][incomingMessage.SequenceNumber]);
                        Console.WriteLine($"Resend {incomingMessage.SequenceNumber}");
                    }else if(_connection.Status == ConnectionStatus.Connected)
                    {
                        HandleMessage(incomingMessage);
                    }
                    

                }
            });
            task.Start();
        }
        
        
        private async Task HandleMessage(CustomProtocolMessage incomingMessage)
        {
            _connection.SendFragmentAcknoledgement(incomingMessage.Id, incomingMessage.SequenceNumber);
           
            // Console.WriteLine($"Incomming message #{incomingMessage.SequenceNumber}");
            if(_fragmentManager.IsFirstFragment(incomingMessage.Id))
            {
                StartCheckingUndeliveredFragments(incomingMessage.Id);
            }
            if(!_fragmentManager.AddFragment(incomingMessage))
            {
                return;
            }
            

            if(_fragmentManager.CheckDeliveryCompletion(incomingMessage.Id))
            {
                StopCheckingUndeliveredFragments(incomingMessage.Id);
                if(!incomingMessage.IsFile)
                {
                    Console.WriteLine("New message:");
                    Console.WriteLine(_fragmentManager.AssembleFragmentsAsText(incomingMessage.Id));
                    _fragmentManager.ClearMessages(incomingMessage.Id);
                }else
                {
                    Console.WriteLine("File received");
                    await _fragmentManager.SaveFragmentsAsFile(incomingMessage.Id);
                    _fragmentManager.ClearMessages(incomingMessage.Id);
                }
            }
        }
            
          
            
            
        
        
        
        
        private Dictionary<UInt16, List<uint>> _unAcknowledgedMessages = new Dictionary<UInt16, List<uint>>();
        
        public async Task SendFile(string filePath,uint fragmentSize = 4)
        {
            string filename = Path.GetFileName(filePath);
            Console.WriteLine(filename);
            await SendMessage([..Encoding.ASCII.GetBytes(filename), ..(await File.ReadAllBytesAsync(filePath))], fragmentSize, true, (UInt16)filename.Length);
        }
        public async Task SendText(string text, uint fragmentSize = 4)
        {
            await SendMessage(Encoding.ASCII.GetBytes(text), fragmentSize);
        }
        public async Task SendMessage(byte[] bytes, uint fragmentSize = 4, bool isFile=false, UInt16 filenameOffset=0)
        {
            Console.WriteLine("sending");
            UInt16 id = (UInt16)Random.Shared.Next(0,UInt16.MaxValue);
            UInt16 seqNum = 0;
            _unAcknowledgedMessages.Add(id, new List<uint>());
            

            await Task.Run(async ()=>
            {
                List<List<CustomProtocolMessage>> fragmentsToSend = new List<List<CustomProtocolMessage>>();
                int currentWindowStart = 0;
                int currentWindowEnd = currentWindowStart+_windowSize-1;
                int currentFragmentListIndex = 0;
                int count = 0;
                fragmentsToSend.Add(new List<CustomProtocolMessage>());
                int seqq = 0;
                for(int i = 0; i < bytes.Length; i+=(int)fragmentSize)
                {   
                    
                    
                    CustomProtocolMessage message = CreateFragment(bytes, i, fragmentSize);
                    message.IsFile = isFile;
                    message.FilenameOffset = filenameOffset;
                    message.SequenceNumber = seqNum;
                    message.Id = id;    
                    if(message.Last)
                    {
                        seqq = message.SequenceNumber;
                    }

                    fragmentsToSend[currentFragmentListIndex].Add(message);
                    count++;
                    if(seqNum == UInt16.MaxValue)
                    {
                        fragmentsToSend.Add(new List<CustomProtocolMessage>());
                        
                        currentFragmentListIndex+=1;
                        seqNum = 0;
                        
                    }else
                    {
                        seqNum++;
                    }

                }
                
                currentFragmentListIndex = 0;
                _currentFragmentsPortions.Add(id, new List<CustomProtocolMessage>());
            
                for(int i = 0; i < fragmentsToSend.Count; i++)
                {
                    Console.WriteLine($"Sending portion {i}");
                    await StartSendingFragments(fragmentsToSend[i], id);
                    
                    currentFragmentListIndex++;
                }
                
                Console.WriteLine("Message sent");
                
            });
                
                
            
        }
       
        private int _windowSize = 100;

        private Dictionary<UInt16,List<CustomProtocolMessage>> _currentFragmentsPortions = new Dictionary<ushort, List<CustomProtocolMessage>>();
        private async Task StartSendingFragments(List<CustomProtocolMessage> currentFragmentsPortion, UInt16 id)
        {
            _currentFragmentsPortions[id] = currentFragmentsPortion;
            int currentWindowStart = 0;
            int currentWindowEnd = currentWindowStart+_windowSize-1;
            int previousWindowEnd = currentWindowStart+_windowSize-1;
            for(int i = currentWindowStart; i <= currentWindowEnd && i < currentFragmentsPortion.Count; i++)
            {

               
                _unAcknowledgedMessages[id].Add(currentFragmentsPortion[i].SequenceNumber);
               // Console.WriteLine($"Sending fragment with sequence #{currentFragmentsPortion[i].SequenceNumber}");
                

                await _connection.SendMessage(currentFragmentsPortion[i]);
               
                
            }
            while(currentWindowEnd < currentFragmentsPortion.Count)
            {
                  
                await WaitForFirstInWindow(currentFragmentsPortion,id, (UInt32)currentWindowStart);
                currentWindowStart+=1;
                
                previousWindowEnd = Int32.Max(currentWindowEnd, previousWindowEnd);
                currentWindowEnd = currentWindowStart+_windowSize-1;
              
        
                for(;previousWindowEnd <= currentWindowEnd && previousWindowEnd < currentFragmentsPortion.Count;previousWindowEnd++)
                {
                 

                    _unAcknowledgedMessages[id].Add(currentFragmentsPortion[previousWindowEnd].SequenceNumber);
            
                    if(currentFragmentsPortion[previousWindowEnd].Last)
                    {
                        Console.WriteLine("Last fragment");
                    }
                    await _connection.SendMessage(currentFragmentsPortion[previousWindowEnd], true);
                 //  Console.WriteLine($"Sending fragment with sequence #{currentFragmentsPortion[previousWindowEnd].SequenceNumber}");

                }
                    
                
                  
                
                

            }
           
            for(int i = 0; i < currentWindowEnd;i++)
            {
                await WaitForFirstInWindow(currentFragmentsPortion,id, (UInt32)(currentWindowStart+i));
            }

            _unAcknowledgedMessages[id].Clear();
         
            
        }
        int _rttThreshold = 350;
        private async Task WaitForFirstInWindow(List<CustomProtocolMessage> fragments,UInt16 id, UInt32 seqNum)
        {
            int delay = 50;
            if(!_unAcknowledgedMessages[id].Contains(seqNum))
            {
             
                return;
            }
            int overralTime = 0;
           

            await Task.Run(async()=>
            {
                int c = 0;
                while(true)
                {
                    await Task.Delay(delay);
                    c++;
                    if(!_unAcknowledgedMessages[id].Contains(seqNum))
                    {
                        if(_windowSize > 1 && overralTime > _rttThreshold)
                        {
                            _windowSize/=2;
                           // Console.WriteLine($"Window decreased {_windowSize}");
                            
                          
                        }else if( overralTime <= _rttThreshold)
                        {
                            _windowSize++;
                           
                            //Console.WriteLine("Window increased");
                        }
                        
                        return;
                    }
                    
                    
                    overralTime+=delay;
                    if(c >= 50)
                    {
                        Console.WriteLine($"Resedning fragment #{seqNum}");
                        await _connection.SendMessage(fragments[(int)seqNum]);
                      
                        c = 0;
                        

                    }
                   

                    
                    
                }
            });

        }
        Dictionary<UInt16, CancellationTokenSource> _checkingUndeliveredFragmentsCancellationTokenSources = new Dictionary<ushort, CancellationTokenSource>();
        public async Task StartCheckingUndeliveredFragments(UInt16 id)
        {
            _checkingUndeliveredFragmentsCancellationTokenSources.Add(id, new CancellationTokenSource());
            await Task.Run(async ()=>
            {
                try
                {
                    while(true)
                    {
                        _checkingUndeliveredFragmentsCancellationTokenSources[id].Token.ThrowIfCancellationRequested();
                        await Task.Delay(500);
                        _checkingUndeliveredFragmentsCancellationTokenSources[id].Token.ThrowIfCancellationRequested();

                        foreach(UInt16 sequenceNumber in _fragmentManager.GetUndeliveredFragments(id))
                        {   
                            Console.WriteLine($"Requested {sequenceNumber}");
                            await _connection.MakeRepeatRequest(sequenceNumber, id);
                        }
                    }
                }catch(OperationCanceledException e)
                {
                    Console.WriteLine("Checking stopped");
                }
            },_checkingUndeliveredFragmentsCancellationTokenSources[id].Token);

        }
        private void StopCheckingUndeliveredFragments(UInt16 id)
        {
            _checkingUndeliveredFragmentsCancellationTokenSources[id].Cancel();
        }
        public CustomProtocolMessage CreateFragment(byte[] bytes, int start, uint fragmentSize)
        {
            CustomProtocolMessage message = new CustomProtocolMessage();
            
            int end = (int)(start+fragmentSize);
        
            if(end > bytes.Length-1)
            {
                Console.WriteLine("Last fragmenr created");
                message.SetFlag(CustomProtocolFlag.Last, true);
            }
            message.Data = bytes.Take(new Range(start, end)).ToArray();
            return message;
        }

        public async Task Connect(ushort port, string address)
        {
            
            await _connection.Connect(port, address);
        
        }
        public void ClearBeforeDisconnection()
        {
            _fragmentManager.ClearAllMessages();
            _unAcknowledgedMessages.Clear();
        }
        public async Task Disconnect()
        {
            ClearBeforeDisconnection();
            Console.WriteLine("Disconnecting...");
            await _connection.Disconnect();
            Console.WriteLine("Disconnected");
            
        }

        


        
    }
}
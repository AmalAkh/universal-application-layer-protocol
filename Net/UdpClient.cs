using System.Diagnostics;
using System.Net;
using Timers = System.Timers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using CustomProtocol.Net.Exceptions;
using NUnit.Framework.Constraints;
using System.Timers;


namespace CustomProtocol.Net
{

    public class CustomUdpClient
    {
       

        
        private Connection _connection;

        protected Socket _sendingSocket;
        protected Socket _listeningSocket;
        

        private DateTime _lastMessageTime = DateTime.Now;
        
        FragmentManager _fragmentManager = new FragmentManager();
        public CustomUdpClient()
        {
           
        }
        public void SetPath(string path)
        {
            _fragmentManager.SavePath = path;
        }
        public bool IsPathEmpty
        {
            get{return _fragmentManager.SavePath == "";}
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
            _connection.Interrupted+= InterruptConnection;
            _fragmentManager.FragmentLost+= async (id, sequnceNumber)=>
            {
                await _connection.MakeRepeatRequest(sequnceNumber, id);

            };
            StartCheckingConnection();
            

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
                        Console.WriteLine($"Fragment #{e.Message.SequenceNumber} damaged");
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
                    }else if((_connection.Status == ConnectionStatus.Connected || _connection.Status == ConnectionStatus.Emergency) && incomingMessage.KeepAlive && incomingMessage.Ack)
                    {
                       

                        _connection.ReceiveKeepAliveResponse();
                        if(_connection.Status == ConnectionStatus.Emergency)
                        {
                            
                            _lastMessageTime = DateTime.Now;
                        
                            _connection.CancelEmergencyCheck();
                        }
                    }else if(_connection.Status == ConnectionStatus.Connected && incomingMessage.Finish)
                    {
                        await _connection.AcceptDisconnection();
                    }
                    else if(_connection.Status == ConnectionStatus.Connected && incomingMessage.Ack && !incomingMessage.Syn)
                    {
                        _lastMessageTime = DateTime.Now;
                        if(_unAcknowledgedMessages.ContainsKey(incomingMessage.Id))
                        {
                            
                            _unAcknowledgedMessages[incomingMessage.Id].RemoveAll((seqNum)=>seqNum==incomingMessage.SequenceNumber);

                        }
                    }else if(incomingMessage.KeepAlive && !incomingMessage.Ack)
                    {
                        
                        await _connection.SendKeepAliveResponse();
                    }else if(incomingMessage.Syn && _connection.Status == ConnectionStatus.Connected)
                    {
                       
                        await _connection.SendMessage(_currentFragmentsPortions[incomingMessage.Id][incomingMessage.SequenceNumber]);
                        
                        //Console.WriteLine($"Resend {incomingMessage.SequenceNumber}");
                    }else if(_connection.Status == ConnectionStatus.Connected)
                    {
                       // Console.WriteLine(_listeningSocket.Available);
                        HandleMessage(incomingMessage);
                    }
                    

                }
            });
            task.Start();
        }
        
        public void StartCheckingConnection()
        {
            Task.Run(async ()=>
            {
                while(true)
                {
                    await Task.Delay(500);
                    if(!_connection.IsTransmitting)
                    {
                        _lastMessageTime = DateTime.Now;
                    }
                   
                   
                    if(_connection.IsTransmitting && _connection.Status == ConnectionStatus.Connected && DateTime.Now.Subtract(_lastMessageTime).Seconds > 5)
                    {

                        Console.WriteLine($"Checking connection...");
                        await _connection.EmergencyCheck();

                    }
                
                }
                
            });
        }
        
        private async Task HandleMessage(CustomProtocolMessage incomingMessage)
        {
            _lastMessageTime = DateTime.Now;
            _connection.SendFragmentAcknoledgement(incomingMessage.Id, incomingMessage.SequenceNumber);
            
            // Console.WriteLine($"Incomming message #{incomingMessage.SequenceNumber}");
            if(_fragmentManager.IsFirstFragment(incomingMessage.Id))
            {
                _connection.StartTransmission();
            
                Console.WriteLine("");
                Console.WriteLine("Incoming message...");
            }
            if(!_fragmentManager.AddFragment(incomingMessage))
            {
                return;
            }

           // Console.WriteLine($"Received fragment #{incomingMessage.SequenceNumber}");
            

            if(_fragmentManager.CheckDeliveryCompletion(incomingMessage.Id))
            {
                _connection.StopTransmission();
                _fragmentManager.StopWatch(incomingMessage.Id);
              //  StopCheckingUndeliveredFragments(incomingMessage.Id);

                Console.WriteLine($"The fragment size: {_fragmentManager.GetFragmentSize(incomingMessage.Id) } bytes");
                Console.WriteLine($"The last fragment size: {_fragmentManager.GetLastFragmentSize(incomingMessage.Id)} bytes");
                if(!incomingMessage.IsFile)
                {
                    Console.WriteLine("New message:");
                    Console.WriteLine(_fragmentManager.AssembleFragmentsAsText(incomingMessage.Id));
                    
                }else
                {
                    Console.WriteLine("File received");
                    Console.WriteLine("File saved here:");
                    Console.WriteLine(await _fragmentManager.SaveFragmentsAsFile(incomingMessage.Id));
                    
                }
                Console.Write("Time:");
                Console.WriteLine(_fragmentManager.GetTransmissionTime(incomingMessage.Id));
                _fragmentManager.ClearMessages(incomingMessage.Id);

            }
        }

        
        private Dictionary<UInt16, List<uint>> _unAcknowledgedMessages = new Dictionary<UInt16, List<uint>>();
        
        public async Task SendFile(string filePath,uint fragmentSize = 4,bool err=false)
        {
            string filename = Path.GetFileName(filePath);
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
            Console.WriteLine($"File size:{fileBytes.Length} bytes");
            await SendMessage([..Encoding.ASCII.GetBytes(filename), ..(fileBytes)], fragmentSize, true, (UInt16)filename.Length, err);
        }
        public async Task SendText(string text, uint fragmentSize = 4,bool err=false)
        {
            await SendMessage(Encoding.ASCII.GetBytes(text), fragmentSize, err:err);
        }
        public async Task SendMessage(byte[] bytes, uint fragmentSize = 4, bool isFile=false, UInt16 filenameOffset=0, bool err=false)
        {
            Console.WriteLine("Sending message...");
            Console.WriteLine($"Fragment size: {fragmentSize} bytes");
            _connection.StartTransmission();
            UInt16 id = (UInt16)Random.Shared.Next(1,UInt16.MaxValue);
            
            _unAcknowledgedMessages.Add(id, new List<uint>());
            int currentWindowStart = 0;
            int currentWindowEnd = currentWindowStart+_windowSize-1;

            await Task.Run(async ()=>
            {
                
                List<List<CustomProtocolMessage>> fragmentsToSend = _fragmentManager.CreateFragments(bytes,id, fragmentSize, filenameOffset, isFile);
                int fragmentsCount = 0;
                foreach(var list in fragmentsToSend)
                {
                    fragmentsCount += list.Count;
                    if(list.Count != 0 && list[list.Count-1].Last)
                    {
                        Console.WriteLine($"Last fragment size:{list[list.Count-1].Data.Length} bytes");
                    }
                }
                
                Console.WriteLine($"Fragments to send: {fragmentsCount}");
                for(int i = 0; i < fragmentsToSend.Count; i++)
                {
                    Console.WriteLine($"Sending portion {i}");
                    await StartSendingFragments(fragmentsToSend[i], id, err);
                   
                }
                if(_connection.Status == ConnectionStatus.Connected)
                {
                    Console.WriteLine("Message sent");
                }
                
                _connection.StopTransmission();

                
            });
                
                
            
        }
       
        private int _windowSize = 10;

        private Dictionary<UInt16,List<CustomProtocolMessage>> _currentFragmentsPortions = new Dictionary<ushort, List<CustomProtocolMessage>>();
        private async Task StartSendingFragments(List<CustomProtocolMessage> currentFragmentsPortion, UInt16 id, bool err=false)
        {
            _currentFragmentsPortions[id] = currentFragmentsPortion;
            int currentWindowStart = 0;
            int currentWindowEnd = currentWindowStart+_windowSize-1;
            int previousWindowEnd = currentWindowStart+_windowSize-1;   

           
            int seqNumForError = Random.Shared.Next(0,currentFragmentsPortion.Count);
           

            for(int i = currentWindowStart; i <= currentWindowEnd && i < currentFragmentsPortion.Count; i++)
            {

                if(_unAcknowledgedMessages.ContainsKey(id))
                {
                    _unAcknowledgedMessages[id].Add(currentFragmentsPortion[i].SequenceNumber);
                }
             //  Console.WriteLine($"Sending fragment with sequence #{currentFragmentsPortion[i].SequenceNumber}");
                
                

                if(_connection.Status == ConnectionStatus.Unconnected)
                {
                    return;
                }
                
                if(err && seqNumForError == i)
                {
                  
                    await _connection.SendMessageWithError(currentFragmentsPortion[i]);

                }else
                {
                    await _connection.SendMessage(currentFragmentsPortion[i], err);
                }
                StartFragmentTimer(currentFragmentsPortion, id, currentFragmentsPortion[i].SequenceNumber);
            
               
               
                
            }
            while(currentWindowEnd < currentFragmentsPortion.Count)
            {
                if(_connection.Status == ConnectionStatus.Unconnected)
                {
                    return;
                }
                await WaitForFragmentAck(currentFragmentsPortion,id, (UInt32)currentWindowStart);
                currentWindowStart+=1;
                
                previousWindowEnd = Int32.Max(currentWindowEnd, previousWindowEnd);
                currentWindowEnd = currentWindowStart+_windowSize-1;
              
                
                for(;previousWindowEnd <= currentWindowEnd && previousWindowEnd < currentFragmentsPortion.Count;previousWindowEnd++)
                {
                    
                    
                  
                    if(_unAcknowledgedMessages.ContainsKey(id))
                    {
                        _unAcknowledgedMessages[id].Add(currentFragmentsPortion[previousWindowEnd].SequenceNumber);
                    }
                   
                    if(_connection.Status == ConnectionStatus.Unconnected)
                    {
                        return;   
                    }
                    if(err && seqNumForError == previousWindowEnd)
                    {
                       
          
                        
                        await _connection.SendMessageWithError(currentFragmentsPortion[previousWindowEnd]);

                    }else
                    {
                        await _connection.SendMessage(currentFragmentsPortion[previousWindowEnd], err);
                    }
                    StartFragmentTimer(currentFragmentsPortion, id, currentFragmentsPortion[previousWindowEnd].SequenceNumber);
                    
                //    Console.WriteLine($"Sending fragment with sequence #{currentFragmentsPortion[previousWindowEnd].SequenceNumber}");

                }
                    
                
                  
                
                

            }
           
            for(int i = 0; i < currentWindowEnd;i++)
            {
                await WaitForFragmentAck(currentFragmentsPortion,id, (UInt32)(currentWindowStart+i));
            }

            _unAcknowledgedMessages[id].Clear();
         
            
        }
        int _rttThreshold = 200;
        private async Task WaitForFragmentAck(List<CustomProtocolMessage> fragments,UInt16 id, UInt32 seqNum)
        {
            int delay = 50;
            if(!_unAcknowledgedMessages[id].Contains(seqNum))
            {
             
                return;
            }
            int overralTime = 0;
            int c = 0;

            await Task.Run(async()=>
            {
                
                while(true)
                {
                    await Task.Delay(delay);
                    c++;
                   
                    if(_connection.Status == ConnectionStatus.Emergency)
                    {
                        continue;
                    }
                    if(_connection.Status == ConnectionStatus.Unconnected)
                    {
                        return;
                    }
                 
                    if(!_unAcknowledgedMessages[id].Contains(seqNum))
                    {
                        if(_windowSize > 1 && overralTime > _rttThreshold)
                        {
                            _windowSize/=2;
                           // Console.WriteLine(_windowSize);
                           // Console.WriteLine($"Window decreased {_windowSize}");
                            
                          
                        }else if( overralTime <= _rttThreshold)
                        {
                            _windowSize++;
                        
                        }
                        
                        return;
                    }
                    
                    
                    overralTime+=delay;                
                    
                }
            });

        }
        private void StartFragmentTimer(List<CustomProtocolMessage> fragments,UInt16 id, UInt32 seqNum)
        {
            System.Timers.Timer timer = new Timers.Timer(2000);
           
            timer.Elapsed += async delegate
            {
                if(!_unAcknowledgedMessages.ContainsKey(id))
                {
                    return;
                }
                if(!_unAcknowledgedMessages[id].Contains(seqNum))
                {
                    timer.Stop();
                    return;
                }
                else
                {
                   // Console.WriteLine("Resend");
                    await _connection.SendMessage(fragments[(int)seqNum]);
                   
                }
            };
            timer.Enabled = true;
            timer.AutoReset = true;
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
        private void InterruptConnection()
        {
            ClearBeforeDisconnection();
            Console.WriteLine("Connection lost");
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
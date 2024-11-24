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
                    }else if(_connection.Status == ConnectionStatus.Connected && incomingMessage.KeepAlive)
                    {
                        _connection.ReceiveKeepAliveResponse();
                        if(_connection.Status == ConnectionStatus.Emergency)
                        {
                            _connection.CancelEmergencyCheck();
                        }
                    }else if(_connection.Status == ConnectionStatus.Connected && incomingMessage.Finish)
                    {
                        await _connection.AcceptDisconnection();
                    }
                    else if(_connection.Status == ConnectionStatus.Connected && incomingMessage.Ack && !incomingMessage.Syn)
                    {
                        if(_unAcknowledgedMessages.ContainsKey(incomingMessage.Id))
                        {
                            _lastMessageTime = DateTime.Now;
                            _unAcknowledgedMessages[incomingMessage.Id].RemoveAll((seqNum)=>seqNum==incomingMessage.SequenceNumber);

                        }
                    }else if(incomingMessage.KeepAlive)
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
                    
                    if(_connection.IsTransmitting && _connection.Status != ConnectionStatus.Unconnected && DateTime.Now.Subtract(_lastMessageTime).Seconds > 3)
                    {
                        Console.WriteLine("Emergency check");
                        await _connection.EmergencyCheck();

                    }
                }
                
            });
        }
        
        private async Task HandleMessage(CustomProtocolMessage incomingMessage)
        {
            _connection.SendFragmentAcknoledgement(incomingMessage.Id, incomingMessage.SequenceNumber);
            _lastMessageTime = DateTime.Now;
            // Console.WriteLine($"Incomming message #{incomingMessage.SequenceNumber}");
            if(_fragmentManager.IsFirstFragment(incomingMessage.Id))
            {
                _connection.StartTransmission();
                StartCheckingUndeliveredFragments(incomingMessage.Id);
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
                StopCheckingUndeliveredFragments(incomingMessage.Id);

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
           
            await SendMessage([..Encoding.ASCII.GetBytes(filename), ..(await File.ReadAllBytesAsync(filePath))], fragmentSize, true, (UInt16)filename.Length, err);
        }
        public async Task SendText(string text, uint fragmentSize = 4,bool err=false)
        {
            await SendMessage(Encoding.ASCII.GetBytes(text), fragmentSize, err:err);
        }
        public async Task SendMessage(byte[] bytes, uint fragmentSize = 4, bool isFile=false, UInt16 filenameOffset=0, bool err=false)
        {
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
                }
                
                Console.WriteLine($"Fragments to send: {fragmentsCount}");
                for(int i = 0; i < fragmentsToSend.Count; i++)
                {
                 //   Console.WriteLine($"Sending portion {i}");
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

            bool errAdded = false;
            int seqNumForError = Random.Shared.Next(0,currentFragmentsPortion.Count);


            for(int i = currentWindowStart; i <= currentWindowEnd && i < currentFragmentsPortion.Count; i++)
            {

                try
                {
                _unAcknowledgedMessages[id].Add(currentFragmentsPortion[i].SequenceNumber);
               // Console.WriteLine($"Sending fragment with sequence #{currentFragmentsPortion[i].SequenceNumber}");
                
                }catch(KeyNotFoundException)
                {
                    return;
                }

                if(_connection.Status == ConnectionStatus.Unconnected)
                {
                    return;
                }
                if(currentFragmentsPortion[i].Last)
                {
                    Console.WriteLine($"The last fragment size: {currentFragmentsPortion[i].Data.Length} bytes");
                }
                if(err && !errAdded && seqNumForError == i)
                {
                    errAdded = true;
                    Console.WriteLine($"at least one {seqNumForError}");
                    await _connection.SendMessageWithError(currentFragmentsPortion[i]);

                }else
                {
                    await _connection.SendMessage(currentFragmentsPortion[i], err);
                }
               
               
                
            }
            while(currentWindowEnd < currentFragmentsPortion.Count)
            {
                if(_connection.Status == ConnectionStatus.Unconnected)
                {
                    return;
                }
                await WaitForFirstInWindow(currentFragmentsPortion,id, (UInt32)currentWindowStart);
                currentWindowStart+=1;
                
                previousWindowEnd = Int32.Max(currentWindowEnd, previousWindowEnd);
                currentWindowEnd = currentWindowStart+_windowSize-1;
              
                
                for(;previousWindowEnd <= currentWindowEnd && previousWindowEnd < currentFragmentsPortion.Count;previousWindowEnd++)
                {
                    
                    
                    //Console.WriteLine(currentFragmentsPortion[previousWindowEnd].SequenceNumber-currentWindowStart);
                    try
                    {
                        _unAcknowledgedMessages[id].Add(currentFragmentsPortion[previousWindowEnd].SequenceNumber);
                    }catch(KeyNotFoundException e)
                    {
                        return;
                    }
                    if(currentFragmentsPortion[previousWindowEnd].Last)
                    {
                        Console.WriteLine($"The last fragment size {currentFragmentsPortion[previousWindowEnd].Data.Length} bytes");
                    }
                    if(_connection.Status == ConnectionStatus.Unconnected)
                    {
                        return;   
                    }
                    if(err && !errAdded && seqNumForError == previousWindowEnd)
                    {
                        errAdded = true;
                        Console.WriteLine($"at least one {seqNumForError}");
                        
                        await _connection.SendMessageWithError(currentFragmentsPortion[previousWindowEnd]);

                    }else
                    {
                        await _connection.SendMessage(currentFragmentsPortion[previousWindowEnd], err);
                    }
                    
                 //  Console.WriteLine($"Sending fragment with sequence #{currentFragmentsPortion[previousWindowEnd].SequenceNumber}");

                }
                    
                
                  
                
                

            }
           
            for(int i = 0; i < currentWindowEnd;i++)
            {
                await WaitForFirstInWindow(currentFragmentsPortion,id, (UInt32)(currentWindowStart+i));
            }

            _unAcknowledgedMessages[id].Clear();
         
            
        }
        int _rttThreshold = 200;
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
                    if(_connection.Status == ConnectionStatus.Emergency)
                    {
                        continue;
                    }
                    if(_connection.Status == ConnectionStatus.Unconnected)
                    {
                        return;
                    }
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
                    if(c >= 40)
                    {
                      //  Console.WriteLine($"Resedning fragment #{seqNum}");
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
                        await Task.Delay(250);
                        _checkingUndeliveredFragmentsCancellationTokenSources[id].Token.ThrowIfCancellationRequested();
                     //   Console.WriteLine(_fragmentManager.GetUndeliveredFragments(id).Count);
                        foreach(UInt16 sequenceNumber in _fragmentManager.GetUndeliveredFragments(id))
                        {   
                            //Console.WriteLine($"Requested {sequenceNumber}");
                            await _connection.MakeRepeatRequest(sequenceNumber, id);
                        }
                    }
                }catch(OperationCanceledException e)
                {
                   // Console.WriteLine("Checking stopped");
                }
            },_checkingUndeliveredFragmentsCancellationTokenSources[id].Token);

        }
        private void StopCheckingUndeliveredFragments(UInt16 id)
        {
            if(_checkingUndeliveredFragmentsCancellationTokenSources.ContainsKey(id))
            {
                _checkingUndeliveredFragmentsCancellationTokenSources[id].Cancel();
            }
        }
        

        public async Task Connect(ushort port, string address)
        {
            
            await _connection.Connect(port, address);
        
        }
        public void ClearBeforeDisconnection()
        {
            _fragmentManager.ClearAllMessages();

            foreach(UInt16 id in _unAcknowledgedMessages.Keys)
            {
                StopCheckingUndeliveredFragments(id);
            }
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
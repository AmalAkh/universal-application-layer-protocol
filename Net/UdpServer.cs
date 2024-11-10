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
    public enum UdpServerStatus
    {
        Unconnected, Connected, WaitingForIncomingConnectionAck, WaitingForOutgoingConnectionAck
    }
    public class CustomUdpClient
    {
       

        
        private Connection _connection;

        protected Socket _sendingSocket;
        protected Socket _listeningSocket;
        public UdpServerStatus status;
        public UdpServerStatus Status
        {
            get;
        }

        private Dictionary<uint, List<CustomProtocolMessage>> _fragmentedMessages = new Dictionary<uint, List<CustomProtocolMessage>>();
        private Dictionary<uint, List<CustomProtocolMessage>> _bufferedFragmentedMessages = new Dictionary<uint, List<CustomProtocolMessage>>();

        FragmentManager _fragmentManager = new FragmentManager();
        public CustomUdpClient()
        {
           
        }
        
        public void Start(string address, ushort listeningPort, ushort sendingPort)
        {
            _listeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
            _listeningSocket.Bind(new IPEndPoint(IPAddress.Parse(address), listeningPort));

            _sendingSocket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
            _sendingSocket.Bind(new IPEndPoint(IPAddress.Parse(address), sendingPort));
            _connection = new Connection(_listeningSocket, _sendingSocket);
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
                     //   Console.WriteLine("Fragment damaged");
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
                          

                            _unAcknowledgedMessages[incomingMessage.Id].Remove(incomingMessage.SequenceNumber);
                        }
                    }else if(incomingMessage.Ping)
                    {
                        await _connection.SendPong();
                    }else if(_connection.Status == ConnectionStatus.Connected)
                    {
                        HandleMessage(incomingMessage);
                    }
                    

                }
            });
            task.Start();
        }
        private Dictionary<UInt16, UInt32> _overrallMessagesCount = new Dictionary<UInt16, UInt32>();
        
        
        private async Task HandleMessage(CustomProtocolMessage incomingMessage)
        {
            if(incomingMessage.Last && incomingMessage.SequenceNumber == 0)
            {
                Console.WriteLine("New message:");
                Console.WriteLine(Encoding.ASCII.GetString(incomingMessage.Data));
            }else 
            {
               // Console.WriteLine($"Incomming message #{incomingMessage.SequenceNumber}");
              
                _fragmentManager.AddFragment(incomingMessage);
                

                if(_fragmentManager.CheckDeliveryCompletion(incomingMessage.Id))
                {
                    Console.WriteLine("New message:");
                    Console.WriteLine(_fragmentManager.AssembleFragmentsAsText(incomingMessage.Id));
                    _fragmentManager.ClearMessages(incomingMessage.Id);
                }
            }
            await Task.Delay(Random.Shared.Next(100,3000));
           // Console.WriteLine($"Acknowledged #{incomingMessage.SequenceNumber}");
            await _connection.SendFragmentAcknoledgement(incomingMessage.Id, incomingMessage.SequenceNumber);
            
        
        }
        
        
        private Dictionary<UInt16, List<uint>> _unAcknowledgedMessages = new Dictionary<UInt16, List<uint>>();
        private int _windowSize = UInt16.MaxValue;
    
       
        public async Task SendTextMessage(string text, uint fragmentSize = 4)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            UInt16 id = (UInt16)Random.Shared.Next(0,UInt16.MaxValue);
            UInt16 seqNum = 0;
            _unAcknowledgedMessages.Add(id, new List<uint>());
            if(bytes.Length <= fragmentSize)
            {
                CustomProtocolMessage message = new CustomProtocolMessage();
                
                message.SetFlag(CustomProtocolFlag.Last, true);
                message.Data = bytes;
                await _connection.SendMessage(message);
                Console.WriteLine("Message sent");
            }else
            {   

                await Task.Run(async ()=>
                {
                    List<List<CustomProtocolMessage>> fragmentsToSend = new List<List<CustomProtocolMessage>>();
                    int currentWindowStart = 0;
                    int currentWindowEnd = currentWindowStart+_windowSize-1;
                    int currentFragmentListIndex = 0;
                    fragmentsToSend.Add(new List<CustomProtocolMessage>());
                    for(ushort i = 0; i < bytes.Length; i+=(ushort)fragmentSize)
                    {   
                       
                        
                        CustomProtocolMessage message = CreateFragment(bytes, i, fragmentSize);
                        message.SequenceNumber = seqNum;
                        message.Id = id;

                        fragmentsToSend[currentFragmentListIndex].Add(message);

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
                    fragmentsToSend[currentFragmentListIndex][fragmentsToSend[currentFragmentListIndex].Count-1].Last = true;
                    currentFragmentListIndex = 0;
                    
                    for(int i = 0; i < fragmentsToSend.Count; i++)
                    {
                       Console.WriteLine($"Sending portion {i}");
                       await StartSendingFragments(fragmentsToSend[i], id);
                       currentFragmentListIndex++;
                    }
                   
                    Console.WriteLine("Message sent");
                });
                
                
            }
        }
        private bool _isWindowChangable = true;
        private async Task StartSendingFragments(List<CustomProtocolMessage> currentFragmentsPortion, UInt16 id)
        {
            int currentWindowStart = 0;
            int currentWindowEnd = currentWindowStart+_windowSize-1;
            for(int i = currentWindowStart; i <= currentWindowEnd && i < currentFragmentsPortion.Count; i++)
            {
                _unAcknowledgedMessages[id].Add(currentFragmentsPortion[i].SequenceNumber);
               // Console.WriteLine($"Sending fragment with sequence #{currentFragmentsPortion[i].SequenceNumber}");
                

                await _connection.SendMessage(currentFragmentsPortion[i]);
               
                
            }
            while(currentWindowEnd < currentFragmentsPortion.Count-1)
            {
                  
                await WaitForFirstInWindow(currentFragmentsPortion,id, (UInt32)currentWindowStart);
                currentWindowStart+=1;
                int previousWindowEnd = currentWindowEnd;
                currentWindowEnd = currentWindowStart+_windowSize-1;
              
        
                for(int j = previousWindowEnd+1; j <= currentWindowEnd && j < currentFragmentsPortion.Count;j++)
                {
                    _unAcknowledgedMessages[id].Add(currentFragmentsPortion[j].SequenceNumber);
            
                    if(currentFragmentsPortion[j].Last)
                    {
                      //  Console.WriteLine("Last fragment");
                    }
                    await _connection.SendMessage(currentFragmentsPortion[j], true);
                  //  Console.WriteLine($"Sending fragment with sequence #{currentFragmentsPortion[j].SequenceNumber}");

                }
                    
                
                  
                
                /*_unAcknowledgedMessages[id].Add(currentFragmentsPortion[currentWindowEnd].SequenceNumber);
              
                if(currentFragmentsPortion[currentWindowEnd].Last)
                {
                    Console.WriteLine("Last fragment");
                }
                await _connection.SendMessage(currentFragmentsPortion[currentWindowEnd], true);
               
                
                Console.WriteLine($"Sending fragment with sequence #{currentFragmentsPortion[currentWindowEnd].SequenceNumber}");*/

            }
            _isWindowChangable = false;
            for(int i = 0; i < _windowSize;i++)
            {
                await WaitForFirstInWindow(currentFragmentsPortion,id, (UInt32)(currentWindowStart+i));
            }
            /*
             await WaitForFirstInWindow(currentFragmentsPortion,id, (UInt32)currentWindowStart+1);
            await WaitForFirstInWindow(currentFragmentsPortion,id, (UInt32)currentWindowStart+2);
            await WaitForFirstInWindow(currentFragmentsPortion,id, (UInt32)currentWindowStart+3);*/
            
        }
        int _rttThreshold = 200;
        private async Task WaitForFirstInWindow(List<CustomProtocolMessage> fragments,UInt16 id, UInt32 seqNum)
        {
            if(!_unAcknowledgedMessages[id].Contains(seqNum))
            {
                if(_isWindowChangable)
                {
                    _windowSize++;
                
                    Console.WriteLine("Window increased");
                }
                return;
            }
            int overralTime = 0;
            int delay = 25;

            await Task.Run(async()=>
            {
                while(true)
                {
                    await Task.Delay(delay);
                    if(!_unAcknowledgedMessages[id].Contains(seqNum))
                    {
                        if(_windowSize > 1 && overralTime > _rttThreshold)
                        {
                            _windowSize--;
                            Console.WriteLine("Window decreased");
                            //delay+=10;
                        }else if( overralTime < _rttThreshold && _isWindowChangable)
                        {
                            _windowSize++;
                            if(delay >50)
                            {
                               // delay-=10;
                            }
                            Console.WriteLine("Window increased");
                        }
                        
                        return;
                    }
                    
                    
                    overralTime+=delay;
                    Console.WriteLine($"Resedning fragment #{seqNum}");
                    await _connection.SendMessage(fragments[(int)seqNum]);
                       

                    
                    
                }
            });

        }
        public CustomProtocolMessage CreateFragment(byte[] bytes, UInt16 start, uint fragmentSize)
        {
            CustomProtocolMessage message = new CustomProtocolMessage();
            
            int end = (int)(start+fragmentSize);
        
            if(end > bytes.Length)
            {
                message.SetFlag(CustomProtocolFlag.Last, true);
            }
            message.Data = bytes.Take(new Range(start, end)).ToArray();
            return message;
        }

        public async Task Connect(ushort port, string address)
        {
            
            await _connection.Connect(port, address);
        
        }
        public async Task Disconnect()
        {
            Console.WriteLine("Disconnecting...");
            await _connection.Disconnect();
            Console.WriteLine("Disconnected");

            
        }

        


        
    }
}
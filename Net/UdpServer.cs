using System.Diagnostics;
using System.Net;
using Timers = System.Timers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;


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
                    //Console.WriteLine($"Received - {receiveFromResult.ReceivedBytes}");

                    CustomProtocolMessage incomingMessage = CustomProtocolMessage.FromBytes(bytes.Take(receiveFromResult.ReceivedBytes).ToArray());
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
                    }else if(incomingMessage.Ping)
                    {
                        await _connection.SendPong();
                    }else if(_connection.Status == ConnectionStatus.Connected)
                    {
                        await HandleMessage(incomingMessage);
                    }
                    

                }
            });
            task.Start();
        }
       
        private async Task HandleMessage(CustomProtocolMessage incomingMessage)
        {
            if(incomingMessage.Last && incomingMessage.SequenceNumber == 0)
            {
                Console.WriteLine("New message:");
                Console.WriteLine(Encoding.ASCII.GetString(incomingMessage.Data));
            }else 
            {
                if(incomingMessage.Last)
                {
                    _fragmentedMessages[incomingMessage.Id].Add(incomingMessage);
                    AssembleFragments(incomingMessage.Id, false);
                }else
                {
                    await AddToFragmentedMessage(incomingMessage);
                }
            }
            
        }
        private async Task AddToFragmentedMessage(CustomProtocolMessage incomingMessage)
        {
            if(_fragmentedMessages.ContainsKey(incomingMessage.Id))
            {
                _fragmentedMessages[incomingMessage.Id].Add(incomingMessage);

            }else
            {
                _fragmentedMessages.Add(incomingMessage.Id, new List<CustomProtocolMessage>());
                _fragmentedMessages[incomingMessage.Id].Add(incomingMessage);

            }
        }
        private async void AssembleFragments(uint id, bool isFile = false)
        {
            List<byte> defragmentedBytes = new List<byte>();
            foreach(CustomProtocolMessage msg in _fragmentedMessages[id])
            {
                foreach(byte oneByte in msg.Data)
                {
                    defragmentedBytes.Add(oneByte);
                }
            }
            if(!isFile)
            {
                Console.WriteLine("New message");
                Console.WriteLine(Encoding.ASCII.GetString(defragmentedBytes.ToArray()));
            }
        }
        private int _windowSize = 4;
        public async Task SendTextMessage(string text, uint fragmentSize = 5)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            UInt16 id = (UInt16)Random.Shared.Next(0,Int16.MaxValue);
            UInt32 seqNum = 0;
           
            if(bytes.Length <= fragmentSize)
            {
                CustomProtocolMessage message = new CustomProtocolMessage();
                
                message.SetFlag(CustomProtocolFlag.Last, true);
                message.Data = bytes;
                await _connection.SendMessage(message);
            }else
            {
             
                int currentWindow = 0;
                for(uint i = 0; i < bytes.Length; i+=fragmentSize)
                {
                    if(currentWindow == 4)
                    {   
                        Console.WriteLine("Window was send");
                        await Task.Delay(2000);
                        currentWindow = 0;
                    }   
                  
                    CustomProtocolMessage message = CreateFragment(bytes, seqNum, fragmentSize);
                 
                    message.Id = id;
                    seqNum++;
                    if(i+fragmentSize > bytes.Length)
                    {
                        message.SetFlag(CustomProtocolFlag.Last, true);
                    }
                    
                   
                    await _connection.SendMessage(message);

                  
                    
                    currentWindow++;
                }
                Console.WriteLine("Transporation ended");
            }
        }
        public CustomProtocolMessage CreateFragment(byte[] bytes, uint sequenceNumber, uint fragmentSize)
        {
            CustomProtocolMessage message = new CustomProtocolMessage();
            message.SequenceNumber = sequenceNumber;
            
            
            
            int start =(int) (sequenceNumber*fragmentSize);
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
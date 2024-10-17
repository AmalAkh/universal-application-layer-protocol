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
                        if(incomingMessage.Last && incomingMessage.SequenceNumber == 0)
                        {
                            Console.WriteLine("New message:");
                            Console.WriteLine(Encoding.ASCII.GetString(incomingMessage.Data));
                        }
                    }
                    else{
                        await _connection.HandleMessage(incomingMessage, receiveFromResult.RemoteEndPoint);
                    }
                    

                }
            });
            task.Start();
        }

        public async Task SendTextMessage(string text, int fragmentSize = 40)
        {
            CustomProtocolMessage message = new CustomProtocolMessage();
            message.SetFlag(CustomProtocolFlag.Last, true);
            message.Data = Encoding.ASCII.GetBytes(text);
            await _connection.SendMessage(message);
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
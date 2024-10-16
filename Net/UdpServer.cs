using System.Diagnostics;
using System.Net;
using Timers = System.Timers;
using System.Net.NetworkInformation;
using System.Net.Sockets;


namespace CustomProtocol.Net
{
    public enum UdpServerStatus
    {
        Unconnected, Connected, WaitingForIncomingConnectionAck, WaitingForOutgoingConnectionAck
    }
    public class UdpServer
    {
       

        
        private Connection _connection;

        protected Socket _sendingSocket;
        protected Socket _listeningSocket;
        public UdpServerStatus status;
        public UdpServerStatus Status
        {
            get;
        }
        public UdpServer()
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
                    
                    //Console.WriteLine($"Received - {receiveFromResult.ReceivedBytes}");

                    CustomProtocolMessage incomingMessage = CustomProtocolMessage.FromBytes(bytes.Take(receiveFromResult.ReceivedBytes).ToArray());

                    await _connection.HandleMessage(incomingMessage, receiveFromResult.RemoteEndPoint);

                }
            });
            task.Start();
        }

        

        public async Task Connect(ushort port, string address)
        {
           
            await _connection.Connect(port, address);

            
        }
        public async Task Disconnect()
        {
            Console.WriteLine("Disconnecting.....");
            await _connection.Disconnect();
            Console.WriteLine("Disconnected");

            
        }

        


        
    }
}
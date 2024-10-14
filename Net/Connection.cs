using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;


namespace CustomProtocol.Net
{
    public class UdpServer
    {
       

        


        protected Socket SendingSocket;
        protected Socket ListeningSocket;
        protected bool IsConnectionEstablished = false;
        public UdpServer()
        {

        }


        public void Start(string address, ushort listeningPort, ushort sendingPort)
        {
            ListeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
            ListeningSocket.Bind(new IPEndPoint(IPAddress.Parse(address), listeningPort));

            SendingSocket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
            SendingSocket.Bind(new IPEndPoint(IPAddress.Parse(address), sendingPort));

            Console.WriteLine($"Listening on address {address} on port {listeningPort}");
            Console.WriteLine($"Listening on address {address} on port {sendingPort}");

        }
        
        protected void StartListening()
        {
            Task task = new Task(async()=>
            {

                    while(true)
                    {
                        byte[] bytes = new byte[1500];
                        IPEndPoint endPoint = new IPEndPoint(IPAddress.None,0);
                        SocketReceiveFromResult receiveFromResult = await ListeningSocket.ReceiveFromAsync(bytes, endPoint);

                        Console.WriteLine($"Received - {receiveFromResult.ReceivedBytes}");

                        CustomProtocolMessage incomingMessage = CustomProtocolMessage.FromBytes(bytes);
                        if(!IsConnectionEstablished &&  incomingMessage.Flags[(int)CustomProtocolFlag.Ack] && !incomingMessage.Flags[(int)CustomProtocolFlag.Syn])
                        {
                            
                                                    
                        }
                    }
            });
            task.Start();
        }
        
        public void Connect(ushort port, string address = "127.0.0.1")
        {
           


        }

        /// <summary>
        /// Get maxumum payload size in bytes available for this network
        /// </summary>
        public static int GetMaximumUdpPayloadSize()
        {
            //in progress
            /*NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach(NetworkInterface adapter in interfaces)
            {

            }*/
            return 1500-60-4;
        }


        
    }
}
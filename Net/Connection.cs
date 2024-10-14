using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;


namespace CustomProtocol.Net
{
    public class UdpServer
    {
        protected ushort listeningPort;
        protected string listeningAddress;

        protected string TargetAddress;
        protected ushort TargetPort;


        protected Socket TargetSocket;
        protected bool IsConnectionEstablished = false;
        public UdpServer(ushort port, string address = "127.0.0.1")
        {
            this.listeningPort = port;
            this.listeningAddress = address;

            
        }
        
        public void StartListening()
        {
            Task task = new Task(async()=>
            {
                    
                var socket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
                
                //Dns.GetHostAddresses(Dns.GetHostName())[0]
                Console.WriteLine($"Listening on address {listeningAddress} on port {listeningPort}");
                
                socket.Bind(new IPEndPoint(IPAddress.Parse(listeningAddress), listeningPort));
              

                    while(true)
                    {
                        byte[] bytes = new byte[1500];
                        IPEndPoint endPoint = new IPEndPoint(IPAddress.None,0);
                        SocketReceiveFromResult receiveFromResult = await socket.ReceiveFromAsync(bytes, endPoint);

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
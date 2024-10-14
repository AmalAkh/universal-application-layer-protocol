using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;


namespace CustomProtocol.Net
{
    public class UdpServer
    {
        protected ushort listeningPort;
        protected string listeningAddress;
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
                        int bytesCount = await socket.ReceiveAsync(bytes);
                        Console.WriteLine($"Received - {bytesCount}");
                    }
            });
            task.Start();
        }
        
        public void Connect(ushort port, string address = "127.0.0.1")
        {
            CustomProtocolMessage synMessage = new CustomProtocolMessage();

            synMessage.SetFlag(CustomProtocolFlag.Syn, true);
            synMessage.Data = [..BitConverter.GetBytes(port)];
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
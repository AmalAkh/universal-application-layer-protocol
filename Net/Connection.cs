using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;


namespace CustomProtocol.Net
{
    public enum UdpServerStatus
    {
        Unconnected, Connected, WaitingForIncomingConnectionAck, WaitingForOutcomingConnectionAck
    }
    public class UdpServer
    {
       

        


        protected Socket SendingSocket;
        protected Socket ListeningSocket;
        public UdpServerStatus status;
        public UdpServerStatus Status
        {
            get;
        }
        public UdpServer()
        {

        }
        protected UInt32 MessageSize;
        protected EndPoint TargetEndPoint;
        protected string TargetAddress;
        
        public void Start(string address, ushort listeningPort, ushort sendingPort)
        {
            ListeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
            ListeningSocket.Bind(new IPEndPoint(IPAddress.Parse(address), listeningPort));

            SendingSocket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
            SendingSocket.Bind(new IPEndPoint(IPAddress.Parse(address), sendingPort));
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
                        byte[] bytes = new byte[1500];
                        IPEndPoint endPoint = new IPEndPoint(IPAddress.None,0);
                        SocketReceiveFromResult receiveFromResult = await ListeningSocket.ReceiveFromAsync(bytes, endPoint);

                        Console.WriteLine($"Received - {receiveFromResult.ReceivedBytes}");

                        CustomProtocolMessage incomingMessage = CustomProtocolMessage.FromBytes(bytes);
                        if(status == UdpServerStatus.Unconnected &&  incomingMessage.Flags[(int)CustomProtocolFlag.Syn] && !incomingMessage.Flags[(int)CustomProtocolFlag.Ack])
                        {
                            
                         
                            TargetEndPoint = receiveFromResult.RemoteEndPoint;
                            MessageSize = BitConverter.ToUInt16(incomingMessage.Data);


                            CustomProtocolMessage ackMessage = new CustomProtocolMessage();
                            ackMessage.SetFlag(CustomProtocolFlag.Ack, true);
                            ackMessage.SetFlag(CustomProtocolFlag.Syn, true);


                            await SendingSocket.SendToAsync(ackMessage.ToByteArray(), TargetEndPoint);

                            status = UdpServerStatus.WaitingForIncomingConnectionAck;
                            Console.WriteLine("Waiting for acknoledgement");


                        }else if(status == UdpServerStatus.WaitingForIncomingConnectionAck && incomingMessage.Flags[(int)CustomProtocolFlag.Ack] && !incomingMessage.Flags[(int)CustomProtocolFlag.Syn])
                        {
                            status = UdpServerStatus.Connected;
                            Console.WriteLine("Connected");
                        }

                    }
            });
            task.Start();
        }
        
        public async Task Connect(ushort port, string address,UInt16 messageSize)
        {
           
            CustomProtocolMessage ackMessage = new CustomProtocolMessage();
            ackMessage.SetFlag(CustomProtocolFlag.Ack, true);
            await SendingSocket.SendToAsync(ackMessage.ToByteArray(), TargetEndPoint);
            
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
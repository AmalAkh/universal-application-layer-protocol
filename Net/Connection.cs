using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
public class UdpServer
{
    protected ushort port;
    protected string address;
    public UdpServer(ushort port, string address = "127.0.0.1")
    {
        this.port = port;
        this.address = address;
    }

    public void StartListening()
    {
        Task task = new Task(async()=>
        {
                
            var socket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
            
            //Dns.GetHostAddresses(Dns.GetHostName())[0]
            Console.WriteLine($"Listening on address {address} on port {port}");
            
            socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));

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
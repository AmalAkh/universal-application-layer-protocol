using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
public class Connection
{
    protected static bool isListening = false;
    public static void StartListening(ushort port, string address = "127.0.0.1")
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
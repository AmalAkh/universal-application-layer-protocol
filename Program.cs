using CustomProtocol.Utils;
using CustomProtocol.Net;
using System.Net;

Console.WriteLine("Write commands");


var msg = new CustomProtocolMessage();
msg.SetFlags(ack:true, ping:true);




/*

Console.Write("Enter port for listening:");
ushort listeningPort = Convert.ToUInt16(Console.ReadLine());
UdpServer udpServer = new UdpServer(listeningPort);
udpServer.StartListening();*/

CRC16 crc16 = new CRC16(0x1021);
Console.WriteLine(Convert.ToString(crc16.Compute([0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39]),16));



while(true)
{
    string command = Console.ReadLine();
    
    if(command.StartsWith("connect"))
    {

        string[] splitedCommand = command.Split();
        string targetIP = splitedCommand[1];

        Dictionary<string,string> cmdArgs = CLIArgsParser.Parse(splitedCommand, 2);
       
        ushort targetPort = Convert.ToUInt16(CLIArgsParser.GetArg(cmdArgs, "-p", "5050"));
        

        
    }

}
using CustomProtocol.Utils;
using CustomProtocol.Net;
using System.Net;
using System.Text;

Console.WriteLine("Write commands");

CustomProtocolMessage message = new CustomProtocolMessage();

message.SequenceNumber = 124;
message.Id= 510;
message.SetFlag(CustomProtocolFlag.Ack, true);
message.Data = Encoding.ASCII.GetBytes("Test message");
byte[] bytes = message.ToByteArray();




var newMsg = CustomProtocolMessage.FromBytes(bytes);

var text = Encoding.ASCII.GetString(newMsg.Data);







/*
Console.Write("Enter address for listening:");
string address = Console.ReadLine();
Console.Write("Enter port for listening:");
ushort listeningPort = Convert.ToUInt16(Console.ReadLine());*/
UdpServer udpServer = new UdpServer();

udpServer.Start("127.0.0.1", 5050, 9911);


while(true)
{
    string command = Console.ReadLine();
    
    if(command.StartsWith("connect"))
    {

        string[] splitedCommand = command.Split();
        string targetIP = splitedCommand[1];

        Dictionary<string,string> cmdArgs = CLIArgsParser.Parse(splitedCommand, 2);
       
        ushort targetPort = Convert.ToUInt16(CLIArgsParser.GetArg(cmdArgs, "-p", "5050"));
        
      
        await udpServer.Connect(targetPort,targetIP, 50);

    }

}
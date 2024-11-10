using CustomProtocol.Utils;
using CustomProtocol.Net;
using System.Net;
using System.Text;

Console.WriteLine("Write commands");




var msg = new CustomProtocolMessage();

var msg2 = CustomProtocolMessage.FromBytes(msg.ToByteArray());






//Console.Write("Enter address for listening:");
//string address = Console.ReadLine();
Console.Write("Enter port for listening:");
ushort listeningPort = Convert.ToUInt16(Console.ReadLine());
Console.Write("Enter port for sending:");
ushort sendingPort = Convert.ToUInt16(Console.ReadLine());
CustomUdpClient udpServer = new CustomUdpClient ();

//udpServer.Start("127.0.0.1", 5050, 9911);
udpServer.Start("127.0.0.1", listeningPort, sendingPort);
//udpServer.Start(address, listeningPort, sendingPort);


while(true)
{
    string command = Console.ReadLine();
    
    if(command.StartsWith("connect"))
    {

        string[] splitedCommand = command.Split(" ");
        string[] targetIPAndPort = splitedCommand[1].Split(":");
      
        await udpServer.Connect(Convert.ToUInt16(targetIPAndPort[1]),targetIPAndPort[0]);

    }else if(command.StartsWith("sendtext"))
    {
        Dictionary<string, string> options = CLIArgsParser.Parse(command);
        uint fragmentSize = options.ContainsKey("-fs") ? Convert.ToUInt32(options["-fs"]) : 1;
        Console.WriteLine(fragmentSize);
        Console.Write("Enter message:");
        string text = Console.ReadLine();
        Console.WriteLine(text);
        await udpServer.SendTextMessage(text, fragmentSize);
    }else if(command.StartsWith("disconnect"))
    {
        await udpServer.Disconnect();
    }else
    {
        Console.WriteLine("Unknown command");
    }

}
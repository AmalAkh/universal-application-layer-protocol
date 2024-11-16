using CustomProtocol.Utils;
using CustomProtocol.Net;
using System.Net;
using System.Text;

Console.WriteLine("Write commands");




var msg = new CustomProtocolMessage();
msg.FilenameOffset = 14;
Console.WriteLine(msg.ToByteArray().Length);
var msg2 = CustomProtocolMessage.FromBytes(msg.ToByteArray());





CustomUdpClient udpServer = new CustomUdpClient ();
Console.Write("Enter address for listening:");
string address = Console.ReadLine();
if(address == "t1")
{
    udpServer.Start("127.0.0.1", 5050,8080);
}else if(address == "t2"){
    udpServer.Start("127.0.0.1", 5656,6565);
    await udpServer.Connect(5050, "127.0.0.1");
}
else if(address == "t24"){
    udpServer.Start("127.0.0.1", 5656,6565);
    await udpServer.Connect(5050, "127.0.0.1");
   // await udpServer.SendFile()
}
else if(address == "t3"){
    udpServer.Start("192.168.1.40", 10080,10080);

}else if(address == "t4")
{
    udpServer.Start("192.168.1.68", 5050,8080);
    
}else
{
    Console.Write("Enter port for listening:");
    ushort listeningPort = Convert.ToUInt16(Console.ReadLine());
    Console.Write("Enter port for sending:");
    ushort sendingPort = Convert.ToUInt16(Console.ReadLine());
  
    udpServer.Start(address, listeningPort, sendingPort);
}

//udpServer.Start("127.0.0.1", 5050, 9911);
//udpServer.Start("127.0.0.1", listeningPort, sendingPort);



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
        await udpServer.SendText(text, fragmentSize);
    }else if(command.StartsWith("sendfile"))
    {

        Dictionary<string, string> options = CLIArgsParser.Parse(command, 2);
        uint fragmentSize = options.ContainsKey("-fs") ? Convert.ToUInt32(options["-fs"]) : 1;
        Console.WriteLine(fragmentSize);

   
        await udpServer.SendFile(command.Split(" ")[1], fragmentSize);
    }
    else if(command.StartsWith("disconnect"))
    {
        await udpServer.Disconnect();
    }else
    {
        Console.WriteLine("Unknown command");
    }

}
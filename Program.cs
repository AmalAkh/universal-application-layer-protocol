using CustomProtocol.Utils;
using CustomProtocol.Net;
using System.Net;
using System.Text;

Console.WriteLine("Write commands");











Console.Write("Enter address for listening:");
string address = Console.ReadLine();
Console.Write("Enter port for listening:");
ushort listeningPort = Convert.ToUInt16(Console.ReadLine());
Console.Write("Enter port for sending:");
ushort sendingPort = Convert.ToUInt16(Console.ReadLine());
CustomUdpClient udpServer = new CustomUdpClient ();

//udpServer.Start("127.0.0.1", 5050, 9911);
//udpServer.Start("127.0.0.1", listeningPort, sendingPort);
udpServer.Start(address, listeningPort, sendingPort);


while(true)
{
    string command = Console.ReadLine();
    
    if(command.StartsWith("connect"))
    {

        string[] splitedCommand = command.Split();
        string targetIP = splitedCommand[1];

        Dictionary<string,string> cmdArgs = CLIArgsParser.Parse(splitedCommand, 2);
       
        ushort targetPort = Convert.ToUInt16(CLIArgsParser.GetArg(cmdArgs, "-p", "5050"));
        
      
        await udpServer.Connect(targetPort,targetIP);

    }else if(command.StartsWith("send"))
    {
        Console.WriteLine("Enter message");
        string text = Console.ReadLine();
        await udpServer.SendTextMessage(text, fragmentSize:500);
    }else if(command.StartsWith("disconnect"))
    {
        await udpServer.Disconnect();
    }

}
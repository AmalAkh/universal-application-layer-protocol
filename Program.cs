using CustomProtocol.Utils;
using System.Net;

Console.WriteLine("Write commands");








Console.Write("Enter port for listening:");
ushort listeningPort = Convert.ToUInt16(Console.ReadLine());
UdpServer udpServer = new UdpServer(listeningPort);
udpServer.StartListening();



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
﻿using CustomProtocol.Utils;
using CustomProtocol.Net;
using System.Net;
using System.Text;
using System.Net.Sockets;

Console.WriteLine("Write commands");




var msg = new CustomProtocolMessage();
msg.FilenameOffset = 14;
Console.WriteLine(msg.ToByteArray().Length);
var msg2 = CustomProtocolMessage.FromBytes(msg.ToByteArray());





CustomUdpClient udpClient = new CustomUdpClient ();
Console.Write("Enter address for listening:");
string address = Console.ReadLine();
if(address == "t1")
{
    udpClient.Start("127.0.0.1", 5050,8080);
}else if(address == "t2"){
    udpClient.Start("127.0.0.1", 5656,6565);
    await udpClient.Connect(5050, "127.0.0.1");
}
else if(address == "t24"){
    udpClient.Start("127.0.0.1", 5656,6565);
    await udpClient.Connect(5050, "127.0.0.1");
   // await udpClient.SendFile()
}
else if(address == "t3"){
    udpClient.Start("192.168.1.40", 10080,10080);

}else if(address == "t4")
{
    udpClient.Start("192.168.1.68", 5050,8080);
    
}else
{
    Console.Write("Enter port for listening:");
    ushort listeningPort = Convert.ToUInt16(Console.ReadLine());
    Console.Write("Enter port for sending:");
    ushort sendingPort = Convert.ToUInt16(Console.ReadLine());
  
    udpClient.Start(address, listeningPort, sendingPort);
}

//udpClient.Start("127.0.0.1", 5050, 9911);
//udpClient.Start("127.0.0.1", listeningPort, sendingPort);



while(true)
{
    string command = Console.ReadLine().Trim().Replace("  ", " ");
    
    if(command.StartsWith("connect"))
    {

        string[] splitedCommand = command.Split(" ");
        string[] targetIPAndPort = splitedCommand[1].Split(":");
      
        await udpClient.Connect(Convert.ToUInt16(targetIPAndPort[1]),targetIPAndPort[0]);

    }else if(command.StartsWith("sendtext"))
    {
        Dictionary<string, string> options = CLIArgsParser.Parse(command);
        bool err = options.ContainsKey("-err");

        uint fragmentSize = options.ContainsKey("-fs") ? Convert.ToUInt32(options["-fs"]) : 10;
        if(fragmentSize > 1449)
        {
            Console.WriteLine("Fragment size is too big");
            continue;

        }
        Console.Write("Enter message:");
        string text = Console.ReadLine();
    
        await udpClient.SendText(text, fragmentSize, err);
    }else if(command.StartsWith("sendfile"))
    {

        Dictionary<string, string> options = CLIArgsParser.Parse(command, 2);
        uint fragmentSize = options.ContainsKey("-fs") ? Convert.ToUInt32(options["-fs"]) : 10;
        string filename = command.Split(" ")[1];
        bool err = options.ContainsKey("-err");
        if(!Path.Exists(filename))
        {
            Console.WriteLine("File does not exist or you do not have right to access it");
            continue;
        }
        if(fragmentSize > 1449)
        {
            Console.WriteLine("Fragment size is too big");
            continue;
        }

   
        await udpClient.SendFile(filename, fragmentSize, err);
    }
    else if(command.StartsWith("disconnect"))
    {
        await udpClient.Disconnect();
    }
    else if(command.StartsWith("savepath"))
    {
        string path = command.Split(" ")[1];

        if(Directory.Exists(path))
        {
            udpClient.SetPath(path);
            Console.WriteLine("Save path saved");
        }else
        {
            Console.WriteLine("Path does not exist or you do not have right to access it");
            
        }


    }
    else
    {
        Console.WriteLine("Unknown command");
    }

}
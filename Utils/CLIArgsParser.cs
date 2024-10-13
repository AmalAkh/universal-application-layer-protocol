
using System.Security.Cryptography.X509Certificates;

namespace CustomProtocol.Utils
{
    public class CLIArgsParser
    {
        public static Dictionary<string, string> Parse(string line, int start = 1)
        {
            Dictionary<string, string> args = new Dictionary<string, string>();
            string[] splitedString = line.Split(" ");
            for(int i = start; i < splitedString.Length;)
            {
                if(i+1 == splitedString.Length || splitedString[i+1].StartsWith("-"))
                {
                    args.Add(splitedString[i], "");
                    i++;
                    
                }else
                {
                    args.Add(splitedString[i], splitedString[i+1]);
                    i+=2;

                }
                
            }
            return args;
        
        }
        public static Dictionary<string, string> Parse(string[] splitedString, int start = 1)
        {
            Dictionary<string, string> args = new Dictionary<string, string>();
        
            for(int i = start; i < splitedString.Length;)
            {
                if(i+1 == splitedString.Length || splitedString[i+1].StartsWith("-"))
                {
                    args.Add(splitedString[i], "");
                    i++;
                    
                }else
                {
                    args.Add(splitedString[i], splitedString[i+1]);
                    i+=2;

                }
                
            }
            return args;
        
        }
        public static string GetArg(Dictionary<string, string> argsList, string name, string defaulValue)
        {
            return argsList.ContainsKey(name) ? argsList[name] : defaulValue;
            
        }
    }
    
}
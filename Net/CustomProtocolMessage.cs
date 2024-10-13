

using System.Net.NetworkInformation;


public class CustomProtocolMessage
{
    public UInt32 SequenceNumber;
    public UInt16 Id;
    protected byte flags;


    protected UInt16 HeaderCheckSum;

    protected byte[] data;
    public byte[] Data
    {
        get
        {
            return data;
        }
        set
        {
            data = value;
        }
    }

    protected UInt16 DataCheckSum;

    public CustomProtocolMessage()
    {

    }
   
    public void SetFlags(bool ack=false,bool sync = false, bool last = false,bool  ping=false,bool  pong = false)
    {
        bool[] bits = {ack, sync, last, ping,pong};
        int power = 7;
        foreach(bool flag in bits)
        {
            flags += Convert.ToByte(Convert.ToByte(Math.Pow(2,power)) * (flag ? 1: 0));
            power--;
        }
       
    }

    public byte[] ToByteArray()
    {
       

        return  [..BitConverter.GetBytes(SequenceNumber), ..BitConverter.GetBytes(id),flags, ..BitConverter.GetBytes(HeaderCheckSum) ,..data, ..BitConverter.GetBytes(DataCheckSum)];
        

        
    }
    
}
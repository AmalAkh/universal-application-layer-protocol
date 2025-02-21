


using System.Collections;
using CustomProtocol.Net.Exceptions;

namespace CustomProtocol.Net
{
    public enum CustomProtocolFlag
    {
        Ack = 0, Syn = 1, Last = 2, KeepAlive=3, File=4, Finish=5
    }
    public class CustomProtocolMessage
    {
        public UInt16 SequenceNumber = 0;
      

        public UInt16 Id;
        public bool[] Flags;

        public UInt16 FilenameOffset;

        protected static CRC16 CRC16Implementation = new CRC16();


        public  byte[] Data;
        public int InternalSequenceNum;// is not actually a part of a protocol, used for sequencing received fragments;

        public UInt16 CheckSum;

        public CustomProtocolMessage()
        {
            Flags = new bool[8];
            Data = new byte[0];
            FilenameOffset = 0;
        }
        public bool Ack
        {
            get
            {
                return Flags[(int)CustomProtocolFlag.Ack];
            }
            set
            {
                Flags[(int)CustomProtocolFlag.Ack] = value;
            }
        }
        public bool Syn
        {
            get
            {
                return Flags[(int)CustomProtocolFlag.Syn];
            }
            set
            {
                Flags[(int)CustomProtocolFlag.Syn] = value;
            }
        }
        public bool IsFile
        {
            get
            {
                return Flags[(int)CustomProtocolFlag.File];
            }
            set
            {
                Flags[(int)CustomProtocolFlag.File] = value;
            }
        }
        public bool Finish
        {
            get
            {
                return Flags[(int)CustomProtocolFlag.Finish];
            }
            set
            {
                Flags[(int)CustomProtocolFlag.Finish] = value;
            }
        }
        public bool Last
        {
            get
            {
                return Flags[(int)CustomProtocolFlag.Last];
            }
            set
            {
                Flags[(int)CustomProtocolFlag.Last] = value;
            }
        }
        public bool KeepAlive
        {
            get
            {
                return Flags[(int)CustomProtocolFlag.KeepAlive];
            }
            set
            {
                Flags[(int)CustomProtocolFlag.KeepAlive] = value;
            }
        }
        
        public void SetFlag(CustomProtocolFlag flag, bool value)
        {
            Flags[Convert.ToInt16(flag)] = value;
        
        }

        public byte[] ToByteArray()
        {
        
            //convert bools to bytes
            int power = 7;
            byte flagsByte = 0;
            foreach(bool flag in Flags)
            {
                flagsByte += Convert.ToByte(Convert.ToByte(Math.Pow(2,power)) * (flag ? 1: 0));
                power--;
            }
           
            CheckSum = CRC16Implementation.Compute([..BitConverter.GetBytes(SequenceNumber), ..BitConverter.GetBytes(Id),flagsByte,..BitConverter.GetBytes(FilenameOffset),..Data]);

            byte[] bytes = [..BitConverter.GetBytes(SequenceNumber), ..BitConverter.GetBytes(Id),flagsByte,..BitConverter.GetBytes(FilenameOffset),..Data, BitConverter.GetBytes(CheckSum)[1],BitConverter.GetBytes(CheckSum)[0]];
         
            return  bytes;
            

            
        }
        public static CustomProtocolMessage FromBytes(byte[] bytes)
        {
            
            CustomProtocolMessage message = new CustomProtocolMessage();
           
            message.SequenceNumber = BitConverter.ToUInt16(new ReadOnlySpan<byte>(bytes, 0,2));
            message.Id = BitConverter.ToUInt16(new ReadOnlySpan<byte>(bytes, 2,2));
            
            BitArray bitArray = new BitArray(new byte[]{bytes[4]});
            bitArray.CopyTo(message.Flags, 0);
            Array.Reverse(message.Flags);

         
            message.FilenameOffset = BitConverter.ToUInt16(new ReadOnlySpan<byte>(bytes, 5,2));
            




            

            message.Data = bytes.Take(new Range(7, bytes.Length -2)).ToArray<byte>();
            if(CRC16Implementation.Compute(bytes) != 0)
            {
                throw new DamagedMessageException(message);
            }
            message.CheckSum =  BitConverter.ToUInt16(new ReadOnlySpan<byte>(bytes, bytes.Length-2,2));
           
            
         
            return message;
        }

      
        
        
    }
}


using System.Data.SqlTypes;

namespace CustomProtocol.Net
{
    public class CRC16
    {
        ushort key;
        ushort[] table;
        public CRC16(ushort key = 0x589)
        {
            this.key = key;
            table = new ushort[256];
            for(ushort i = 0; i < 256;i++)
            {
                ushort tmp = (ushort)(i << 8);
                for(int j = 0; j < 8;j++)
                {
                    if((tmp & 0x8000) != 0)
                    {
                        tmp <<=1;
                        tmp ^= key;
                    }else
                    {
                        tmp<<=1;
                    }
                }
                table[i] = tmp;
            }
            
        }
        public ushort Compute(byte[] data)
        {
            ushort crc = 0;
            foreach(byte b in data)
            {
                crc = (ushort)((crc<<8)^table[(crc>>8)^b]);
            }
            return crc;
        }
        
    }
}

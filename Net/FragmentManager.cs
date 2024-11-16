using System;
using System.Linq;
using System.Text;
using NUnit.Framework.Interfaces;

namespace CustomProtocol.Net
{
    public class FragmentManager
    {
        private Dictionary<uint, HashSet<uint>> _receivedSequenceNumbers = new Dictionary<uint, HashSet<uint>>();
        private Dictionary<uint, int> _portionsCounts = new Dictionary<uint, int>();

        
        private Dictionary<uint, List<CustomProtocolMessage>> _fragmentedMessages = new Dictionary<uint, List<CustomProtocolMessage>>();
        
        private Dictionary<UInt16, Int32> _overrallMessagesCount = new Dictionary<UInt16, Int32>();
    
        public FragmentManager()
        {

        }
        public bool CheckDeliveryCompletion(UInt16 id)
        {
            
           // Console.WriteLine(_receivedSequenceNumbers[id].Count);
          
          /*  if(_overrallMessagesCount[id] != -1)
            {
                Console.WriteLine("");
                Console.WriteLine("Missing:");
                for(int i = 0; i < _overrallMessagesCount[id];i++)
                {
                   if(!_fragmentedMessages[id].Exists((msg)=>msg.SequenceNumber == i))
                   {
                    Console.Write($"#{i} ");
                   }
                }
                Console.WriteLine("");

            }*/
            return _overrallMessagesCount[id] != -1 && _overrallMessagesCount[id] == _fragmentedMessages[id].Count;
        }
        public bool CheckSequenceNumberExcess(UInt16 id)
        {
            return _fragmentedMessages.ContainsKey(id)  && _receivedSequenceNumbers[id].Count == UInt16.MaxValue+1;
        }
        public bool AddFragment(CustomProtocolMessage incomingMessage)
        {
            
            if(_fragmentedMessages.ContainsKey(incomingMessage.Id))
            {
               
                if(!_receivedSequenceNumbers[incomingMessage.Id].Add(incomingMessage.SequenceNumber))
                {
                    return false;
                }

            }else
            {
                _fragmentedMessages.Add(incomingMessage.Id, new List<CustomProtocolMessage>());
                
                _overrallMessagesCount.Add(incomingMessage.Id, -1);
                _portionsCounts.Add(incomingMessage.Id, 0);

                
                _receivedSequenceNumbers.Add(incomingMessage.Id, new HashSet<uint>());
                _receivedSequenceNumbers[incomingMessage.Id].Add(incomingMessage.SequenceNumber);
                
                
            }
            incomingMessage.InternalSequenceNum = incomingMessage.SequenceNumber+_portionsCounts[incomingMessage.Id]*(UInt16.MaxValue+1);
      //      Console.WriteLine(incomingMessage.InternalSequenceNum);
            _fragmentedMessages[incomingMessage.Id].Add(incomingMessage);
            if(CheckSequenceNumberExcess(incomingMessage.Id))
            {
                _portionsCounts[incomingMessage.Id]++; 
              //  Console.WriteLine(_portionsCounts[incomingMessage.Id]*UInt16.MaxValue);
                _receivedSequenceNumbers[incomingMessage.Id].Clear();
            }
            if(incomingMessage.Last)
            {
                _overrallMessagesCount[incomingMessage.Id] = _portionsCounts[incomingMessage.Id]*(UInt16.MaxValue+1) +incomingMessage.SequenceNumber+1;
                Console.WriteLine(_portionsCounts[incomingMessage.Id]);
                Console.WriteLine(incomingMessage.SequenceNumber+1);
                Console.WriteLine((Int16.MaxValue+1));

                Console.WriteLine(_fragmentedMessages[incomingMessage.Id].Count);
                Console.WriteLine(_overrallMessagesCount[incomingMessage.Id]);
            }
            return true;
        }
        
        public string AssembleFragmentsAsText(uint id)
        {
            List<byte> defragmentedBytes = new List<byte>();
            _fragmentedMessages[id] = _fragmentedMessages[id].OrderBy((fragment)=>fragment.InternalSequenceNum).ToList();
            
          
            foreach(CustomProtocolMessage msg in _fragmentedMessages[id])
            {
                foreach(byte oneByte in msg.Data)
                {
                    defragmentedBytes.Add(oneByte);
                }
            }
            
            Console.WriteLine("New message:");
            return Encoding.ASCII.GetString(defragmentedBytes.ToArray());
            
        }

        public async Task<string> SaveFragmentsAsFile(uint id)
        {
            List<byte> defragmentedBytes = new List<byte>();
            _fragmentedMessages[id] = _fragmentedMessages[id].OrderBy((fragment)=>fragment.InternalSequenceNum).ToList();
            
          
            foreach(CustomProtocolMessage msg in _fragmentedMessages[id])
            {
                foreach(byte oneByte in msg.Data)
                {
                    defragmentedBytes.Add(oneByte);
                }
            } 
            int filenameOffset = _fragmentedMessages[id].Where((msg,index)=>index==0).Select((msg)=>msg.FilenameOffset).FirstOrDefault();
            string filename = Encoding.ASCII.GetString(defragmentedBytes.Take(filenameOffset).ToArray());  
            
            using(FileStream fileStream = new FileStream(Path.Combine("./received_files/", filename), FileMode.Create, FileAccess.Write))
            {
               
                await fileStream.WriteAsync(defragmentedBytes.Take(new Range(filenameOffset, defragmentedBytes.Count)).ToArray());
            }
            
            Console.WriteLine(filename);
            return filename;
            
        }
        public List<List<CustomProtocolMessage>> CreateFragments(byte[] bytes, UInt16 id,uint fragmentSize=4)
        {
            List<List<CustomProtocolMessage>> fragmentsToSend = new List<List<CustomProtocolMessage>>();
            UInt16 seqNum = 0;
            int currentFragmentListIndex = 0;
            fragmentsToSend.Add(new List<CustomProtocolMessage>());
            for(ushort i = 0; i < bytes.Length; i+=(ushort)fragmentSize)
            {   
                
                
                CustomProtocolMessage message = CreateFragment(bytes, i, fragmentSize);
                message.SequenceNumber = seqNum;
                message.Id = id;

                fragmentsToSend[currentFragmentListIndex].Add(message);

                if(seqNum == UInt16.MaxValue)
                {
                    fragmentsToSend.Add(new List<CustomProtocolMessage>());
                    
                    currentFragmentListIndex+=1;
                    seqNum = 0;
                }else
                {
                    seqNum++;
                }

            }
            return fragmentsToSend;
        }
        public CustomProtocolMessage CreateFragment(byte[] bytes, UInt16 start, uint fragmentSize)
        {
            CustomProtocolMessage message = new CustomProtocolMessage();
            
            int end = (int)(start+fragmentSize);
        
            if(end > bytes.Length)
            {
                message.SetFlag(CustomProtocolFlag.Last, true);
            }
            message.Data = bytes.Take(new Range(start, end)).ToArray();
            return message;
        }
        public void ClearMessages(UInt16 id)
        {
            
            _fragmentedMessages.Remove(id);
            _overrallMessagesCount.Remove(id);
            _receivedSequenceNumbers[id].Clear();

        }
        public void ClearAllMessages()
        {
            _fragmentedMessages.Clear();
            _overrallMessagesCount.Clear();
            _receivedSequenceNumbers.Clear();
        }

                
    }
}
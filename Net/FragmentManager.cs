using System;
using System.Linq;
using System.Text;
using Timers = System.Timers;
using NUnit.Framework.Interfaces;
using System.Diagnostics;

namespace CustomProtocol.Net
{
    public class FragmentManager
    {



        private Dictionary<uint, HashSet<uint>> _receivedSequenceNumbers = new Dictionary<uint, HashSet<uint>>();
        private Dictionary<uint, int> _portionsCounts = new Dictionary<uint, int>();

        private Dictionary<UInt16, Stopwatch> _watches = new Dictionary<ushort, Stopwatch>();
        
        private Dictionary<uint, List<CustomProtocolMessage>> _fragmentedMessages = new Dictionary<uint, List<CustomProtocolMessage>>();
        
        private Dictionary<UInt16, Int32> _overrallMessagesCount = new Dictionary<UInt16, Int32>();
    
        private Dictionary<UInt16, HashSet<UInt16>> _undeliveredFragments = new Dictionary<ushort, HashSet<ushort>>();

   
        private string _savePath = "";
        public string SavePath
        {
            get
            {
                return _savePath;
            }
            set
            {
                _savePath = value;
            }
        }
        public int GetLastFragmentSize(ushort id)
        {
            return _fragmentedMessages[id].Where((msg)=>msg.Last).First().Data.Length;
        }
        public int GetFragmentSize(ushort id)
        {
            return _fragmentedMessages[id][0].Data.Length;
        }
        public int GetFragmentsCount(ushort id)
        {
            return _fragmentedMessages.Count;
        }
        public bool IsFirstFragment(UInt16 id)
        {
            return _fragmentedMessages.Count == 0;
        }
        public List<UInt16> GetUndeliveredFragments(UInt16 id)
        {
            return _undeliveredFragments[id].ToList();
        }
        public event Action<UInt16,UInt16> FragmentLost;
        public FragmentManager()
        {

        }
        public void StopWatch(UInt16 id)
        {
            _watches[id].Stop();
        }
        public string GetTransmissionTime(UInt16 id)
        {
            var ts = _watches[id].Elapsed;
       
            return String.Format("{0:00}:{1:00}.{2:00}",
            ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);

        }
        public bool CheckDeliveryCompletion(UInt16 id)
        {
            
            
            if( _overrallMessagesCount[id] != -1)
            {
                Console.WriteLine("----------");
                Console.WriteLine(_fragmentedMessages[id].Count);
                Console.WriteLine(_overrallMessagesCount[id]);
            }
            return (_overrallMessagesCount[id] != -1 && _overrallMessagesCount[id] == _fragmentedMessages[id].Count ) || (_overrallMessagesCount[id] != -1 && _overrallMessagesCount[id] == _fragmentedMessages[id].Count-1);
            
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
                _undeliveredFragments.Add(incomingMessage.Id, new HashSet<ushort>());

                _watches.Add(incomingMessage.Id, new Stopwatch());
                _watches[incomingMessage.Id].Start();

                _receivedSequenceNumbers.Add(incomingMessage.Id, new HashSet<uint>());
                _receivedSequenceNumbers[incomingMessage.Id].Add(incomingMessage.SequenceNumber);
                
                
            }
            
            _undeliveredFragments[incomingMessage.Id].Remove(incomingMessage.SequenceNumber);
        
            for(int i = incomingMessage.SequenceNumber-1; i >= 0 ;i++)
            {
              
                if(!_receivedSequenceNumbers[incomingMessage.Id].Contains((UInt16)i))
                {   
                    
                    //_undeliveredFragments[incomingMessage.Id].Add((UInt16)i);
                    FragmentLost?.Invoke(incomingMessage.Id, incomingMessage.SequenceNumber);
                }else
                {
                   
                    break;
                }
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
               Console.WriteLine("Last frag");
               
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
            string pathToSave = Path.Combine(SavePath, filename);
            using(FileStream fileStream = new FileStream(pathToSave, FileMode.Create, FileAccess.Write))
            {
               
                await fileStream.WriteAsync(defragmentedBytes.Take(new Range(filenameOffset, defragmentedBytes.Count)).ToArray());
            }
            
            
            return Path.GetFullPath(pathToSave);
            
        }
        public List<List<CustomProtocolMessage>> CreateFragments(byte[] bytes, UInt16 id, uint fragmentSize,ushort filenameOffset, bool isFile)
        {
            List<List<CustomProtocolMessage>> fragmentsToSend = new List<List<CustomProtocolMessage>>();
          
            int currentFragmentListIndex = 0;
            int count = 0;
            fragmentsToSend.Add(new List<CustomProtocolMessage>());
            UInt16 seqNum = 0;
            for(int i = 0; i < bytes.Length; i+=(int)fragmentSize)
            {   
                
                
                CustomProtocolMessage message = CreateFragment(bytes, i, fragmentSize);
                message.IsFile = isFile;
                message.FilenameOffset = filenameOffset;
                message.SequenceNumber = seqNum;
                message.Id = id;    
                

                fragmentsToSend[currentFragmentListIndex].Add(message);
                count++;
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
        public CustomProtocolMessage CreateFragment(byte[] bytes, int start, uint fragmentSize)
        {
            CustomProtocolMessage message = new CustomProtocolMessage();
            
            int end = (int)(start+fragmentSize);
        
            if(end > bytes.Length-1)
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
            _watches.Remove(id);

        }
        public void ClearAllMessages()
        {
            _fragmentedMessages.Clear();
            _overrallMessagesCount.Clear();
            _receivedSequenceNumbers.Clear();
            _portionsCounts.Clear();
            _watches.Clear();
        }

                
    }
}
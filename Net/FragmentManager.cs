using System;
using System.Text;
using NUnit.Framework.Interfaces;

namespace CustomProtocol.Net
{
    public class FragmentManager
    {
        private Dictionary<uint, List<CustomProtocolMessage>> _fragmentedMessages = new Dictionary<uint, List<CustomProtocolMessage>>();
        private Dictionary<uint, List<CustomProtocolMessage>> _bufferedFragmentedMessages = new Dictionary<uint, List<CustomProtocolMessage>>();

        private Dictionary<UInt16, UInt32> _overrallMessagesCount = new Dictionary<UInt16, UInt32>();
    
        public FragmentManager()
        {

        }
        public bool CheckDeliveryCompletion(UInt16 id)
        {
            

            return _overrallMessagesCount[id] != 0 && _overrallMessagesCount[id] == _fragmentedMessages[id].Count;
        }
        public bool CheckSequenceNumberExcess(UInt16 id)
        {
            return _fragmentedMessages.ContainsKey(id)  && _fragmentedMessages[id].Count == UInt16.MaxValue + 1;
        }
        public void AddFragment(CustomProtocolMessage incomingMessage)
        {
            
            if(_fragmentedMessages.ContainsKey(incomingMessage.Id))
            {
                _fragmentedMessages[incomingMessage.Id].Add(incomingMessage);   
            }else
            {
                _fragmentedMessages.Add(incomingMessage.Id, new List<CustomProtocolMessage>());
                _overrallMessagesCount.Add(incomingMessage.Id, 0);
                _fragmentedMessages[incomingMessage.Id].Add(incomingMessage);
            }
            if(CheckSequenceNumberExcess(incomingMessage.Id))
            {
                BufferMessages(incomingMessage.Id);
            }
            if(incomingMessage.Last)
            {
                _overrallMessagesCount[incomingMessage.Id] = (UInt16)(incomingMessage.SequenceNumber+1);
            }
        }
        private void BufferMessages(UInt16 id)
        {
             _fragmentedMessages[id] = _fragmentedMessages[id].OrderBy((fragment)=>fragment.SequenceNumber).ToList();
            if(!_bufferedFragmentedMessages.ContainsKey(id))
            {
       
                _bufferedFragmentedMessages.Add(id, new List<CustomProtocolMessage>());
                
            }
            foreach(var fragmentMsg in _fragmentedMessages[id])
            {
                fragmentMsg.InternalSequenceNum = (ulong)(fragmentMsg.SequenceNumber + UInt16.MaxValue * (int)(_bufferedFragmentedMessages[id].Count/UInt16.MaxValue));
                _bufferedFragmentedMessages[id].Add(fragmentMsg);
            }
            _fragmentedMessages[id].Clear();
        }
        public string AssembleFragmentsAsText(uint id)
        {
            List<byte> defragmentedBytes = new List<byte>();
            _fragmentedMessages[id] = _fragmentedMessages[id].OrderBy((fragment)=>fragment.SequenceNumber).ToList();
            
            if(_bufferedFragmentedMessages.ContainsKey(id))
            {
                
                foreach(CustomProtocolMessage msg in _bufferedFragmentedMessages[id].OrderBy((fragment)=>fragment.InternalSequenceNum).ToList())
                {
                    foreach(byte oneByte in msg.Data)
                    {
                      
                        defragmentedBytes.Add(oneByte);
                    }
                }
            }
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
            _bufferedFragmentedMessages.Remove(id);
            _fragmentedMessages.Remove(id);
        }

                
    }
}
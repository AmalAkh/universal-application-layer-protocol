using System.Diagnostics;
using System.Net;
using Timers = System.Timers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;


namespace CustomProtocol.Net
{
    public enum ConnectionStatus
    {
        Unconnected, Connected, WaitingForIncomingConnectionAck, WaitingForOutgoingConnectionAck, WaitingForDisconnection
    }
    public class Connection
    {
       

        


        private Socket _sendingSocket;
        private Socket _listeningSocket;
        private ConnectionStatus _status = ConnectionStatus.Unconnected;
        public ConnectionStatus Status
        {
            get
            {
                return _status;
            }
        }
        public Connection(Socket listeningSocket,Socket sendingSocket)
        {
            this._sendingSocket = sendingSocket;
            this._listeningSocket = listeningSocket;
        }

        private EndPoint _currentEndPoint;
        protected string TargetAddress;


        private int _connectionTimeout = 20000;
        public int ConnectionTimeout
        {
            get
            {
                return _connectionTimeout;
            }
            set
            {
                if(value > 0)
                {
                    _connectionTimeout = value;
                }
            }
        }
        public bool IsConnectionTimeout
        {
            get
            {
                return ( _currentConnectionTime > _connectionTimeout) && ( _status == ConnectionStatus.WaitingForIncomingConnectionAck  || _status == ConnectionStatus.WaitingForOutgoingConnectionAck);
            }
        }
        public bool IsConnectionInterrupted
        {
            get
            {
                return _unrespondedPingPongRequests > 3;
            }
        }
        private int _currentConnectionTime = 0;
        

        private int _unrespondedPingPongRequests = 0;
        
        
       

        CancellationTokenSource _connectionTimerTokenCancallationSource;
        protected async Task StartConnectionTimer()
        {
            try
            {
                _connectionTimerTokenCancallationSource = new CancellationTokenSource();
                await Task.Run(async ()=>
                {

                    while(_currentConnectionTime <= _connectionTimeout)
                    {
                        _connectionTimerTokenCancallationSource.Token.ThrowIfCancellationRequested();
                        await Task.Delay(100);
                        _connectionTimerTokenCancallationSource.Token.ThrowIfCancellationRequested();
                    
                        _currentConnectionTime+=100;
                        
                        
                    }
                    Console.WriteLine("Connection timeout");
                },_connectionTimerTokenCancallationSource.Token);
            }catch(Exception e)
            {

            }
        }
        protected void StopConnectionTimer()
        {
            _currentConnectionTime = 0; 
            _connectionTimerTokenCancallationSource.Cancel();
           
            
        }
        public async Task Connect(ushort port, string address)
        {
            _currentConnectionTime = 0;
            CustomProtocolMessage ackMessage = new CustomProtocolMessage();
            ackMessage.SetFlag(CustomProtocolFlag.Syn, true);
            ackMessage.Data = BitConverter.GetBytes((_listeningSocket.LocalEndPoint as IPEndPoint).Port);
            _status = ConnectionStatus.WaitingForOutgoingConnectionAck;
            await _sendingSocket.SendToAsync(ackMessage.ToByteArray(), new IPEndPoint(IPAddress.Parse(address), port));
     
            
            
            Console.WriteLine("Trying to connect...");
            await StartConnectionTimer();
        }
        public async Task Disconnect()
        {
            if(Status == ConnectionStatus.Connected)
            {
                CustomProtocolMessage disconnectionMessage = new CustomProtocolMessage();
                disconnectionMessage.SetFlag(CustomProtocolFlag.Finish, true);
                await _sendingSocket.SendToAsync(disconnectionMessage.ToByteArray(), _currentEndPoint);
            }
        }
        private CancellationTokenSource _pingPongCancellationTokenSource = new CancellationTokenSource();
        public async Task StartPingPong()
        {
            try
            {
                _pingPongCancellationTokenSource = new CancellationTokenSource();
                await Task.Run(async ()=>
                {
                    _pingPongCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await Task.Delay(5000);
                    _pingPongCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    while(_unrespondedPingPongRequests < 3)
                    {
                    
                        CustomProtocolMessage pingMessage = new CustomProtocolMessage();
                        pingMessage.SetFlag(CustomProtocolFlag.Ping, true);
                        await _sendingSocket.SendToAsync(pingMessage.ToByteArray(), _currentEndPoint);
                        _unrespondedPingPongRequests+=1;
                        await Task.Delay(5000);
                        
                    }
                    Console.WriteLine("Disconnected from host");
                

                }, _pingPongCancellationTokenSource.Token);
            }catch(Exception e)
            {

            }
        }
        private void StopPingPong()
        {
            _pingPongCancellationTokenSource.Cancel();
            _unrespondedPingPongRequests = 0;
        }
        public async Task SendMessage(CustomProtocolMessage message, bool err = false)
        {   
            byte[] bytes =message.ToByteArray();
            if(err && Random.Shared.NextDouble() > 0.8)
            {
                bytes[0] = 12;
            }
            await _sendingSocket.SendToAsync(bytes, _currentEndPoint);
        }
        public async Task HandleMessage(CustomProtocolMessage message, EndPoint senderEndPoint)
        {

        }
        public async Task MakeRepeatRequest(uint sequenceNumber, UInt16 id)
        {
            CustomProtocolMessage synMessage = new CustomProtocolMessage();
            synMessage.SequenceNumber = sequenceNumber;
            synMessage.Id = id;
            synMessage.SetFlag(CustomProtocolFlag.Syn, true);
            await _sendingSocket.SendToAsync(synMessage.ToByteArray(), _currentEndPoint);

        }
        public async Task SendFragmentAcknoledgement(UInt16 id,uint sequenceNumber)
        {
            CustomProtocolMessage synMessage = new CustomProtocolMessage();
            synMessage.SequenceNumber = sequenceNumber;
            synMessage.Id = id;
            synMessage.SetFlag(CustomProtocolFlag.Ack, true);
            await _sendingSocket.SendToAsync(synMessage.ToByteArray(), _currentEndPoint);

        }
        public async Task AcceptConnection(EndPoint senderEndPoint)
        {
            CustomProtocolMessage ackMessage = new CustomProtocolMessage();
            ackMessage.SetFlag(CustomProtocolFlag.Ack, true);
            ackMessage.SetFlag(CustomProtocolFlag.Syn, true);
            _currentEndPoint = senderEndPoint;
            _status = ConnectionStatus.WaitingForIncomingConnectionAck;
            ackMessage.Data = BitConverter.GetBytes((_listeningSocket.LocalEndPoint as IPEndPoint).Port);
            await _sendingSocket.SendToAsync(ackMessage.ToByteArray(), _currentEndPoint);
             
            StartConnectionTimer();
            
            Console.WriteLine("Waiting for acknoledgement");
        }
        public async Task EstablishOutgoingConnection(IPEndPoint senderEndPoint)
        {
            CustomProtocolMessage ackMessage = new CustomProtocolMessage();
            ackMessage.SetFlag(CustomProtocolFlag.Ack, true);
            _currentEndPoint = senderEndPoint;
            _status = ConnectionStatus.Connected;
            await _sendingSocket.SendToAsync(ackMessage.ToByteArray(), _currentEndPoint);

                
            StopConnectionTimer();
            StartPingPong();
            Console.WriteLine("Connected");


            await _sendingSocket.SendToAsync(ackMessage.ToByteArray(), _currentEndPoint);
        }
        public void ReceivePong()
        {
            _unrespondedPingPongRequests-=1;
        }
        public async Task SendPong()
        {
            CustomProtocolMessage pongMessage = new CustomProtocolMessage();
            pongMessage.SetFlag(CustomProtocolFlag.Pong, true);
            await _sendingSocket.SendToAsync(pongMessage.ToByteArray(), _currentEndPoint);
        }
        public async Task AcceptDisconnection()
        {
            StopPingPong();
            _status = ConnectionStatus.Unconnected;
            Console.WriteLine("Disconnected");
        }
        public async Task EstablishConnection()
        {
            _status = ConnectionStatus.Connected;
            StopConnectionTimer();
            StartPingPong();
            Console.WriteLine("Connected");
        }
        public async Task InterruptConnectionHandshake()
        {
            _status = ConnectionStatus.Unconnected;
            _currentEndPoint = null;
            StopConnectionTimer();  
        }
        public async Task InterruptConnection()
        {
            StopPingPong();
            _unrespondedPingPongRequests = 0;
            _status = ConnectionStatus.Unconnected;
            _currentEndPoint = null;
        }
        


        
    }
}
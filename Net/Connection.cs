using System.Diagnostics;
using System.Net;
using Timers = System.Timers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework.Internal.Execution;
using System.Threading;

namespace CustomProtocol.Net
{
    public enum ConnectionStatus
    {
        Unconnected, Connected, WaitingForIncomingConnectionAck, WaitingForOutgoingConnectionAck, WaitingForDisconnection, Emergency
       
    }
    public class Connection
    {
        private int _transmissions = 0;
       
        public delegate void ConnectionEventHandler();

        public event ConnectionEventHandler Interrupted;
        public event ConnectionEventHandler Disconnected;



        


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
        public bool IsTransmitting
        {
            get
            {
                return _transmissions > 0;
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
        public void StartTransmission()
        {
            
            _transmissions++;
        }
        public void StopTransmission()
        {
            
            
            _transmissions--;
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

                StopSendingKeepAlive();
                
                _unrespondedPingPongRequests = 0;
                _status = ConnectionStatus.Unconnected;
                _currentEndPoint = null;  
            }
            
        }
        private CancellationTokenSource _pingPongCancellationTokenSource = new CancellationTokenSource();
        public async Task StartSendingKeepAlive()
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
                        if(!IsTransmitting)
                        {
                            
                            await SendKeepAliveMessage();
                          
                            _unrespondedPingPongRequests+=1;
                            _pingPongCancellationTokenSource.Token.ThrowIfCancellationRequested();
                            await Task.Delay(5000);
                            _pingPongCancellationTokenSource.Token.ThrowIfCancellationRequested();
                        }
                        
                    }//169.254.78.38
                    
                    InterruptConnection();
                   
                

                }, _pingPongCancellationTokenSource.Token);
            }catch(OperationCanceledException)
            {
                
            }
        }
        public async Task SendKeepAliveMessage()
        {
            CustomProtocolMessage pingMessage = new CustomProtocolMessage();
            pingMessage.SetFlag(CustomProtocolFlag.KeepAlive, true);
           

            await _sendingSocket.SendToAsync(pingMessage.ToByteArray(), _currentEndPoint);
        }
        private CancellationTokenSource _emergenecyCheckCancellationTokenSource = new CancellationTokenSource();
        public async Task EmergencyCheck()
        {
            
            _status = ConnectionStatus.Emergency;
            try
            {
                await Task.Run(async ()=>
                {
                    int overralTime = 0;
                   
                    await SendKeepAliveMessage();
                    await SendKeepAliveMessage();
                    await SendKeepAliveMessage();
                    
                    _emergenecyCheckCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    while(overralTime <= 3000)
                    {
                        await Task.Delay(100);
                        overralTime+=100;
                        _emergenecyCheckCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    }
                    _emergenecyCheckCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    
                    await InterruptConnection();

                    

                    
                }, _emergenecyCheckCancellationTokenSource.Token);
            }catch(OperationCanceledException e)
            {
                Console.WriteLine("Cancelled");
                _status = ConnectionStatus.Connected;
            }
        }
        public void CancelEmergencyCheck()
        {
            _emergenecyCheckCancellationTokenSource.Cancel();
        }
        private void StopSendingKeepAlive()
        {
            _pingPongCancellationTokenSource.Cancel();
            _unrespondedPingPongRequests = 0;
            
        }
        public async Task SendMessage(CustomProtocolMessage message, bool err = false)
        {   
            try
            {
                byte[] bytes = message.ToByteArray();
                if(err && Random.Shared.NextDouble() > 0.9999)
                {
                    Console.WriteLine($"Error {message.SequenceNumber}");
                    bytes[Random.Shared.Next(7,bytes.Length)] = (byte)(Random.Shared.Next(0, 256));
                }
                await _sendingSocket.SendToAsync(bytes, _currentEndPoint);
            }catch(SocketException e)
            {
             //   Console.WriteLine($"Fatal error: {e.Message}");
              //  InterruptConnection();
            }
        }
        public async Task SendMessageWithError(CustomProtocolMessage message)
        {   
            try
            {
                byte[] bytes = message.ToByteArray();
                
            //    Console.WriteLine($"Error {message.SequenceNumber}");
                bytes[Random.Shared.Next(7,bytes.Length)] = (byte)(Random.Shared.Next(0, 256));
                
                await _sendingSocket.SendToAsync(bytes, _currentEndPoint);
            }catch(SocketException e)
            {
               // Console.WriteLine($"Fatal error: {e.Message}");
                //InterruptConnection();
            }
        }
        
     
        public async Task MakeRepeatRequest(UInt16 sequenceNumber, UInt16 id)
        {
            CustomProtocolMessage synMessage = new CustomProtocolMessage();
            synMessage.SequenceNumber = sequenceNumber;
            synMessage.Id = id;
            synMessage.SetFlag(CustomProtocolFlag.Syn, true);
            await _sendingSocket.SendToAsync(synMessage.ToByteArray(), _currentEndPoint);

        }
        public async Task SendFragmentAcknoledgement(UInt16 id,UInt16 sequenceNumber)
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
            StartSendingKeepAlive();
            Console.WriteLine("Connected");


            await _sendingSocket.SendToAsync(ackMessage.ToByteArray(), _currentEndPoint);
        }
        public void ReceiveKeepAliveResponse()
        {
            _unrespondedPingPongRequests = 0;
        }
        public async Task SendKeepAliveResponse()
        {
            if(_currentEndPoint != null)
            {
                CustomProtocolMessage pongMessage = new CustomProtocolMessage();
                pongMessage.SetFlag(CustomProtocolFlag.KeepAlive, true);
                pongMessage.SetFlag(CustomProtocolFlag.Ack, true);

                await _sendingSocket.SendToAsync(pongMessage.ToByteArray(), _currentEndPoint);
            }
        }
        public async Task AcceptDisconnection()
        {
            StartSendingKeepAlive();
            _status = ConnectionStatus.Unconnected;
            Console.WriteLine("Disconnected");
        }
        public async Task EstablishConnection()
        {
            _status = ConnectionStatus.Connected;
            StopConnectionTimer();
            StartSendingKeepAlive();
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
            
            StopSendingKeepAlive();
            _unrespondedPingPongRequests = 0;
            _status = ConnectionStatus.Unconnected;
            _currentEndPoint = null;
            _transmissions = 0;
            Interrupted();
           
        }
        


        
    }
}
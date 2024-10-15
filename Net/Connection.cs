using System.Diagnostics;
using System.Net;
using Timers = System.Timers;
using System.Net.NetworkInformation;
using System.Net.Sockets;


namespace CustomProtocol.Net
{
    public enum ConnectionStatus
    {
        Unconnected, Connected, WaitingForIncomingConnectionAck, WaitingForOutgoingConnectionAck, WaitingForDisConnectionAck
    }
    public class Connection
    {
       

        


        private Socket _sendingSocket;
        private Socket _listeningSocket;
        private ConnectionStatus _status;
        public ConnectionStatus Status
        {
            get;
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
        private int _currentConnectionTime = 0;
        

        private int _unrespondedPingPongRequests = 0;
        
        
       

        CancellationTokenSource connectionTimerTokenCancallationSource = new CancellationTokenSource();
        protected async Task StartConnectionTimer()
        {
            
            await Task.Run(async ()=>
            {

                while(_currentConnectionTime <= _connectionTimeout)
                {
                    await Task.Delay(100);
                    _currentConnectionTime+=100;
                    if(connectionTimerTokenCancallationSource.Token.IsCancellationRequested)
                    {
                        connectionTimerTokenCancallationSource.Token.ThrowIfCancellationRequested();
                    }
                }
                Console.WriteLine("Connection timeout");
            },connectionTimerTokenCancallationSource.Token);
        }
        protected void StopConnectionTimer()
        {
            if(connectionTimerTokenCancallationSource != null)
            {
                connectionTimerTokenCancallationSource.Cancel();
                _currentConnectionTime = 0;
            }
        }
        public async Task Connect(ushort port, string address)
        {
           
            CustomProtocolMessage ackMessage = new CustomProtocolMessage();
            ackMessage.SetFlag(CustomProtocolFlag.Ack, true);
            await _sendingSocket.SendToAsync(ackMessage.ToByteArray(), new IPEndPoint(IPAddress.Parse(address), port));
            _status = ConnectionStatus.WaitingForOutgoingConnectionAck;
            
            _status = ConnectionStatus.WaitingForOutgoingConnectionAck;
            Console.WriteLine("Trying to connect...");
            await StartConnectionTimer();
        }
        public async Task StartPingPong()
        {
            
            await Task.Run(async ()=>
            {
                
               await Task.Delay(5000);
                while(_unrespondedPingPongRequests < 3)
                {
                    
                    CustomProtocolMessage pingMessage = new CustomProtocolMessage();
                    pingMessage.SetFlag(CustomProtocolFlag.Ping, true);
                    await _sendingSocket.SendToAsync(pingMessage.ToByteArray(), _currentEndPoint);
                    _unrespondedPingPongRequests+=1;
                    
                }
                Console.WriteLine("Disconnected from host");
               

            });
        }

        public async Task HandleMessage(CustomProtocolMessage message, EndPoint senderEndPoint)
        {
            if(( _currentConnectionTime > _connectionTimeout || _unrespondedPingPongRequests > 3) && ( _status == ConnectionStatus.WaitingForIncomingConnectionAck  || _status == ConnectionStatus.WaitingForOutgoingConnectionAck))
            {   
                await InterruptConnectionHandshake();
            }
            if(_status == ConnectionStatus.Unconnected &&  message.Flags[(int)CustomProtocolFlag.Syn] && !message.Flags[(int)CustomProtocolFlag.Ack])
            {
                            
                
                await AcceptConnection(senderEndPoint);

            }else if(_status == ConnectionStatus.WaitingForIncomingConnectionAck && message.Flags[(int)CustomProtocolFlag.Ack] && !message.Flags[(int)CustomProtocolFlag.Syn])
            {
                                          
                await EstablishConnection();
            }
            else if(_status == ConnectionStatus.Connected && message.Flags[(int)CustomProtocolFlag.Pong])
            {
         
                _unrespondedPingPongRequests-=1;
            }else if(_status == ConnectionStatus.Connected)
            {
                    
            }

        }

        private async Task AcceptConnection(EndPoint senderEndPoint)
        {
            CustomProtocolMessage ackMessage = new CustomProtocolMessage();
            ackMessage.SetFlag(CustomProtocolFlag.Ack, true);
            ackMessage.SetFlag(CustomProtocolFlag.Syn, true);
            _currentEndPoint = senderEndPoint;

            await _sendingSocket.SendToAsync(ackMessage.ToByteArray(), _currentEndPoint);
            StartConnectionTimer();
                            
            _status = ConnectionStatus.WaitingForIncomingConnectionAck;
            Console.WriteLine("Waiting for acknoledgement");
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
        


        
    }
}
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Yachu.Server;
using Yachu.Server.Packets;
using Yachu.Server.Util;

namespace Yachu.Server {
    public class ConnectionHandler {
        public Client Client { get; set; }

        private SocketAsyncEventArgs _receiveEvent;
        private SocketAsyncEventArgs _sendEvent;
        private readonly PacketBuilder _packetBuilder = new();
        public Socket Socket { get; set; }
        private readonly object _receivedPacketQueueLock = new();
        private readonly object _sendPacketQueueLock = new();
        private readonly Queue<Packet> _receivedPacketQueue = new(50);
        private readonly Queue<Packet> _sendPacketQueue = new(100);
        private PacketHandler<Client> _packetHandler;

        public void Initialize(Socket socket) {
            Socket = socket;
            socket.NoDelay = true;
            socket.ReceiveTimeout = 60 * 1000;
            socket.SendTimeout = 3 * 1000;
            // 종료 시 alert 보내기 위해 LingerOption 1초 설정
            socket.LingerState = new LingerOption(true, 1);

            _receiveEvent = SocketAsyncEventArgsPool.Instance.Pop();
            _receiveEvent.Completed += OnReceiveCompleted;
            _receiveEvent.UserToken = this;
            BufferManager.Instance.SetBuffer(_receiveEvent);
            
            _sendEvent = SocketAsyncEventArgsPool.Instance.Pop();
            _sendEvent.Completed += OnSendCompleted;
            _sendEvent.UserToken = this;
            BufferManager.Instance.SetBuffer(_sendEvent);

            _packetHandler = PacketHandler<Client>.Instance;
        }

        public void StartReceive() {
            if (Socket == null) {
                return;
            }
            var pending = Socket.ReceiveAsync(_receiveEvent);
            if (!pending) OnReceiveCompleted(this, _receiveEvent);
        }

        public void AddReceivePacket(Packet packet) {
            lock (_receivedPacketQueueLock) {
                _receivedPacketQueue.Enqueue(packet);
            }
        }

        public void Send<T>(PacketData<T> packetData) where T : PacketData<T> {
            Send(packetData.ToPacket());
        }

        public void Send(Packet packet) {
            if(Socket == null) return;
            lock (_sendPacketQueueLock) {
                // 가능하면 바로 보냄
                _sendPacketQueue.Enqueue(packet);
                if (_sendPacketQueue.Count <= 1) {
                    SendProcess();
                }
            }
        }

        private void SendProcess() {
            if(Socket == null) return;
            var packet = _sendPacketQueue.Peek();
            var sendData = packet.Data;
            var dataLength = packet.Length;
            
            if(packet.Type != (ushort)PacketType.GameCupUpdate && packet.Type != (ushort)PacketType.GameDiceUpdate)
                Log.Info($"Sending to {Client} {dataLength} bytes ({((PacketType)packet.Type).ToString()})");

            // 최대 버퍼 크기보다 크게 보내려는 경우
            if (dataLength > Constants.SocketBufferSize) {
                var sendEvent = SocketAsyncEventArgsPool.Instance.Pop();
                if (sendEvent == null) {
                    Log.Error("SocketAsyncEventArgsPool::Pop() return null");
                    return;
                }

                sendEvent.Completed += DisposeSocketEvent;
                sendEvent.UserToken = this;
                sendEvent.SetBuffer(sendData, 0, dataLength);

                bool pending = Socket.SendAsync(sendEvent);
                if(!pending) DisposeSocketEvent(null, sendEvent);
            }
            else {
                // 최대 버퍼 크기보다 작게 보내는 경우 (대부분의 경우)
                // 보내려는 버퍼 길이를 패킷 길이로 설정
                _sendEvent.SetBuffer(_sendEvent.Offset, dataLength);
                // 데이터 복사
                Array.Copy(
                    sendData, 0, 
                    _sendEvent.Buffer!, _sendEvent.Offset, 
                    dataLength
                );
                
                bool pending = Socket.SendAsync(_sendEvent);
                if(!pending) OnSendCompleted(null, _sendEvent);

            }

        }

        // 게임 로직 스레드(TickProcess)에서 틱마다 호출
        public void Tick() {
            if (_receivedPacketQueue.Count < 0) {
                return;
            }
            lock (_receivedPacketQueueLock) {
                try {
                    while (_receivedPacketQueue.Count > 0) {
                        _packetHandler.HandlePacket(_receivedPacketQueue.Dequeue(), Client);
                    }
                    _receivedPacketQueue.Clear();
                }
                catch (Exception e) {
                    Log.Info(e);
                }
            }
        }
        // ReadAsync의 Completed에 연결
        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs e) {
            // 원격 호스트가 제대로 보냈는지 체크, false면 연결 종료 처리임 
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success) {
                // 패킷 변환 후, receivedPacketQueue에 삽입
                _packetBuilder.OnReceive(e.Buffer, e.Offset, e.BytesTransferred, AddReceivePacket);
                // 반복
                StartReceive();
                return;
            }
            
            if (e.SocketError == SocketError.Success) {
                // Log.Info("SocketError.Success, but ByteTransferred <= 0");
            }
            else {
                Log.Error($"SocketError while receive from {Client}: {e.SocketError.ToString()}");
            }

            // 수신 객체 해제
            {
                if (_receiveEvent.BufferList != null) {
                    _receiveEvent.BufferList = null;
                }

                _receiveEvent.UserToken = null;
                _receiveEvent.RemoteEndPoint = null;
                _receiveEvent.Completed -= OnReceiveCompleted;
                BufferManager.Instance.FreeBuffer(_receiveEvent);
                
                SocketAsyncEventArgsPool.Instance.Push(_receiveEvent);
                _receiveEvent = null;
            }
            AddReceivePacket(new Packet(PacketType.UserClosed));
        }
        
        // SendProcess에서 sendEvent로 보낸 게 아닌 임의로 생성했을 경우, 이 쪽으로 옴
        private void DisposeSocketEvent(object sender, SocketAsyncEventArgs e) {
            OnSendCompleted(sender, e);
            if (e.BufferList != null) e.BufferList = null;
            e.SetBuffer(null, 0, 0);
            e.UserToken = null;
            e.RemoteEndPoint = null;
            e.Completed -= DisposeSocketEvent;
            SocketAsyncEventArgsPool.Instance.Push(e);
        }
        
        private void OnSendCompleted(object sender, SocketAsyncEventArgs e) {
            if ((Client?.IsDisconnected ?? true) || e == null) {
                return;
            }
            if (e.SocketError == SocketError.Success) {
                lock (_sendPacketQueueLock) {
                    // 성공했으므로 Queue에서 삭제
                    if (_sendPacketQueue.Count > 0) _sendPacketQueue.Dequeue();
                    // 아직 남아있으면 반복
                    if (_sendPacketQueue.Count > 0) SendProcess();
                }
            }
            else {
                Log.Error($"Send failed to {Client}: {e.SocketError}");
            }
        }

        public void Close() {
            try {
                if (Socket != null) {
                    Socket.Disconnect(false);
                    // Socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch (Exception e) {
                Log.Info(e);
            }
            finally {
                if (Socket != null) {
                    Socket.Close();
                    Socket = null;
                }
            }

            Socket = null;
            Client = null;
            _packetBuilder.ClearBuffer();

            lock (_sendPacketQueueLock) {
                _sendPacketQueue.Clear();
            }

            lock (_receivedPacketQueueLock) {
                _receivedPacketQueue.Clear();
            }
            // 송신 객체 해제
            {
                BufferManager.Instance.FreeBuffer(_sendEvent);
                if (_sendEvent.BufferList != null) {
                    _sendEvent.BufferList = null;
                }

                _sendEvent.UserToken = null;
                _sendEvent.RemoteEndPoint = null;
                _sendEvent.Completed -= OnSendCompleted;
                
                SocketAsyncEventArgsPool.Instance.Push(_sendEvent);
                _sendEvent = null;
            }
        }
    }
}
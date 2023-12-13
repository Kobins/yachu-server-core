using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Yachu.Server.Util;

namespace Yachu.Server {
    //https://shakddoo.tistory.com/entry/c-%EC%8B%A4%EC%8B%9C%EA%B0%84-%EA%B2%8C%EC%9E%84-%EC%84%9C%EB%B2%84-%EB%A7%8C%EB%93%A4%EA%B8%B0-3-%EC%84%9C%EB%B2%84?category=362471
    public class ClientListener {
        private SocketAsyncEventArgs _acceptEvent;
        private Socket _listenSocket;
        private AutoResetEvent _flowControlEvent; // 접속 safe하게 처리할 수 있도록
        private volatile bool _threadAlive;

        public delegate void ClientAcceptHandler(Socket clientSocket, object token);

        public ClientAcceptHandler CallbackOnClientAccept;

        public ClientListener() {
            CallbackOnClientAccept = null;
            _threadAlive = true;
        }

        public void Start(string host, int port, int backlog = int.MaxValue) {
            Log.Info($"프로토콜 버전: {Constants.ProtocolVersion}");
            Log.Info($"서버 {host}:{port}로 활성화 중 ...");
            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.NoDelay = true; // Nagle 비활성화

            var address = host == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(host);
            var endPoint = new IPEndPoint(address, port);

            try {
                _listenSocket.Bind(endPoint);
                _listenSocket.Listen(backlog);

                _acceptEvent = new SocketAsyncEventArgs();
                _acceptEvent.Completed += OnAcceptCompleted;

                var listenThread = new Thread(DoListen) {
                    Name = "Client Accept Thread"
                };
                listenThread.Start();
                Log.Info($"서버 활성화! {endPoint}에서 Listening 중 ...");
            }
            catch (Exception e) {
                Log.Error(e);
                Log.Error($"서버 활성화 실패 ...");
                throw;
            }
        }

        private void DoListen() {
            _flowControlEvent = new AutoResetEvent(false);
            while (_threadAlive) {
                _acceptEvent.AcceptSocket = null;
                bool pending;
                try {
                    // 비동기로 Accept
                    // 동기 -> false, 비동기 -> true
                    // 비동기 접속 성공 시 Completed(OnAcceptCompleted) 실행
                    pending = _listenSocket.AcceptAsync(_acceptEvent);
                }
                catch (Exception e) {
                    Log.Info(e);
                    continue;
                }
                // 동기로 왔으면
                if (!pending) {
                    OnAcceptCompleted(null, _acceptEvent);
                }

                _flowControlEvent.WaitOne();
            }
        }

        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs e) {
            if (e.SocketError == SocketError.Success) {
                var clientSocket = e.AcceptSocket;
                CallbackOnClientAccept.Invoke(clientSocket, e);
                // NetworkManager.Instance.OnNewClient(clientSocket, e); // 새 클라이언트 접속
            }
            else {
                // 실패했을 때 처리
                Log.Error($"Accepting client failed: {e.SocketError}");
            }

            _flowControlEvent.Set();
        }

        public void Close() {
            _threadAlive = false;
            
            _listenSocket.Close();
            _listenSocket = null;
            
            _acceptEvent.Completed -= OnAcceptCompleted;
            _acceptEvent.AcceptSocket = null;
            _acceptEvent = null;
            
            _flowControlEvent.Dispose();
            _flowControlEvent = null;

            CallbackOnClientAccept = null;
        }

    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using Yachu.Server.Database;
using Yachu.Server.Util;

namespace Yachu.Server {
    public class YachuGameServer : IDisposable {
        public enum ServerState {
            Initialized,
            Running,
            Disposing,
        }
        
        public static YachuGameServer Instance { get; private set; }
        public static IDatabaseAdapter Database => Instance.DatabaseAdapter;
        public int Port { get; }
        public ServerState State { get; private set; } = ServerState.Initialized;
        public IDatabaseAdapter DatabaseAdapter { get; private set; }
        private BufferManager _bufferManager;
        private ClientListener _clientListener;
        public object ClientListLock { get; } = new();
        public List<Client> Clients { get; } = new(Constants.MaxConnection);
        public ConcurrentDictionary<Guid, Client> ClientByGuid { get; } = new(
            Environment.ProcessorCount, 
            Constants.MaxConnection
        );

        public YachuGameServer(bool dedicate = true, int serverPort = Constants.DefaultPort) {
            Constants.DedicatedServer = dedicate;
            Port = serverPort;
            _bufferManager = BufferManager.Instance;
            _clientListener = new ClientListener();
        }

        public void Start() {
            if (State != ServerState.Initialized) {
                return;
            }
            State = ServerState.Running;

            _clientListener.Start("0.0.0.0", Port);
            _clientListener.CallbackOnClientAccept += OnAccept;
            GameRoom.Initialize();

            // 호스트 서버는 방 한개만 사용, 바로 접속 처리
            if (!Constants.DedicatedServer) {
                _callbackOnNewClient += (client) => {
                    var primaryRoom = GameRoom.TryFindRoomOrNull();
                    if (primaryRoom == null) {
                        Log.Error("호스트 서버 상태에서 접속 가능한 room을 찾지 못함");
                        return;
                    }
                    
                    primaryRoom.JoinClient(client);
                };
            }
            else
            {
                DatabaseAdapter = MySqlDatabaseAdapter.Instance;
            }

            Instance = this;
        }

        /// <summary>
        /// 게임 서버 사용자 측에서 실행해주는 업데이트 메소드입니다.
        /// 콘솔 형태의 데디케이티드 서버인 경우 별도의 Tick Thread로 사용됩니다.
        /// 클라이언트에서 가동하는 경우 유니티의 메인 스레드 Update로 처리합니다.
        /// </summary>
        public void Tick() {
            if (State != ServerState.Running) {
                return;
            }

            if (Clients.Count > 0) {
                lock (ClientListLock) {
                    // 클라이언트 전체 순회 Tick
                    foreach (var client in Clients) {
                        client.Tick();
                    }

                    // 연결 해제된 클라이언트 목록에서 제거
                    Clients.RemoveAll(it =>
                    {
                        var remove = it.IsDisconnected;
                        if (remove)
                        {
                            if(it.Guid != Guid.Empty)
                                ClientByGuid.TryRemove(it.Guid, out _);
                        }
                        return remove;
                    });
                }
            }
        }

        public void Dispose() {
            if (State == ServerState.Disposing) {
                return;
            }
            State = ServerState.Disposing;
            _clientListener.Close();
            lock (ClientListLock) {
                foreach (var client in Clients) {
                    client.Close();
                }
                Clients.Clear();
            }
            ClientByGuid.Clear();
        }

        public delegate void NewClientHandler(Client client);

        public NewClientHandler _callbackOnNewClient;
        public void OnAccept(Socket clientSocket, object e) {
            // 연결 초기화
            var connection = new ConnectionHandler();
            connection.Initialize(clientSocket);
            
            // 클라이언트 객체 초기화
            var client = new Client(); // TODO 풀링?
            connection.Client = client;
            client.Initialize(connection);
            
            // 패킷 받기 시작
            connection.StartReceive();

            // 클라이언트 목록에 추가
            lock (ClientListLock) {
                Clients.Add(client);
            }
            
            _callbackOnNewClient?.Invoke(client);
        }
    }
}
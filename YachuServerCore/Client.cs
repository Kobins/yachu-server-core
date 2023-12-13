using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Yachu.Server;
using Yachu.Server.Database;
using Yachu.Server.Packets;
using Yachu.Server.Packets.Body;
using Yachu.Server.Util;

namespace Yachu.Server {
    public class Client {
        public enum ClientState {
            /// <summary>
            /// 서버에 접속하지 않은 상태
            /// </summary>
            Disconnected,
            /// <summary>
            /// 연결 확립, 근데 아직 HandShake는 안 보냄: 버전 일치 필요
            /// </summary>
            HandShaking,
            /// <summary>
            /// 서버에 접속했으나 로그인은 하지 않은 상태 - 회원가입 또는 로그인 필요 
            /// </summary>
            NotLoggedIn,
            /// <summary>
            /// 로그인 또는 회원가입 요청을 DB에서 처리하길 기다리는 상태
            /// </summary>
            LoggingIn,
            /// <summary>
            /// 서버에 접속하여 특정한 UUID를 가지고 있는 상태
            /// </summary>
            Connected,
        }

        public enum ClientType
        {
            Anonymous,
            Registered,
        }

        public ClientState State { get; private set; } = ClientState.Disconnected;
        public bool IsConnected => State == ClientState.Connected;
        public bool IsDisconnected => State == ClientState.Disconnected;
        public ClientType Type { get; private set; }
        public Guid Guid { get; private set; } = Guid.Empty;
        public string Name { get; private set; } = "";
        public UserData CachedUserData { get; set; } = new();

        public ClientData ClientData => new()
        {
            guid = Guid, 
            name = Name, 
            registered = Type == ClientType.Registered
        };

        public GameRoom JoiningRoom { get; set; } = null;

        // 클라이언트 소켓 관련 객체
        public ConnectionHandler ConnectionHandler { get; private set; }

        static Client() {
            RegisterPacketListeners();
        }

        private long _acceptedTimeStamp;
        public void Initialize(ConnectionHandler connection) {
            ConnectionHandler = connection;
            // Accept 완료: HandShaking 단계로 진입.
            // 클라이언트에서 C2SHandShake로 일정 시간 내에 로그인 정보 전송 필요
            State = ClientState.HandShaking;
            _acceptedTimeStamp = ExtraUtil.CurrentTimeInMillis;

            Log.Info($"Connected New Client: {this}");
        }

        // C2SHandShake 전송
        private void HandShake(Packet packet) {
            var data = packet.GetPacketData<C2SHandshakePacket>();
            // 프로토콜 버전 검사
            var clientVersion = data.Version;
            const int serverVersion = Constants.ProtocolVersion;
            if (clientVersion != serverVersion) {
                SendAlert(serverVersion > clientVersion 
                    ? $"클라이언트의 버전이 낮습니다. 업데이트가 필요합니다. ({clientVersion} < {serverVersion})" 
                    : $"프로토콜 버전이 일치하지 않습니다. ({clientVersion} > {serverVersion})"
                );
                Log.Info($"Client {this} disconnected by version(server={serverVersion}, client={clientVersion})");
                Close();
                return;
            }

            State = ClientState.NotLoggedIn;
            Log.Info($"Client {this} Successfully Handshake!");
            SendPacket(new S2CHandshakePacket());
        }

        private void Login(Packet packet)
        {
            if (State != ClientState.NotLoggedIn)
            {
                Log.Warn($"{this} tried to login when State {State.ToString()} != NotLoggedIn");
                return;
            }
            var data = packet.GetPacketData<C2SLoginPacket>();

            /* // 언젠가 비로그인 게스트도 ?
            if (Name.Length <= 0) {
                Name = $"Anonymous_{ExtraUtil.GetRandomAlphabetsAndNumbers()}";
                // 익명계정은 별도로 데이터베이스 호출하지 않음.
                Guid = Guid.NewGuid();
                Type = ClientType.Anonymous;
                State = ClientState.Connected;
                
                Log.Info($"New Anonymous Client Connected: {this}");
                SendPacket(new S2CHandshakePacket{ClientData = ClientData});
                return;
            }
            */

            State = ClientState.LoggingIn;
            Type = ClientType.Registered;
            Log.Info($"{this} Trying Login with {data.Name} ...");
            Task.Run(async () =>
            {
                try
                {
                    var clientData = await YachuGameServer.Database.Login(data.Name, data.HashedPassword);
                    Guid = clientData.guid;
                    if (!YachuGameServer.Instance.ClientByGuid.TryAdd(Guid, this))
                    {
                        State = ClientState.NotLoggedIn;
                        Log.Info($"{this} failed to login : Already Connected ID {data.Name}");
                        SendAlert("로그인에 실패했습니다; 이미 접속중인 ID입니다.");
                        SendPacket(new S2CLoginPacket(S2CLoginPacket.LoginResult.AlreadyLoggedOn, ClientData));
                        return;
                    }
                    Name = data.Name;
                    State = ClientState.Connected;
                    Log.Info($"Successfully Logged in {this}");
                    SendAlert($"어서오세요, {Name} 님!");
                    SendPacket(new S2CLoginPacket(S2CLoginPacket.LoginResult.Success, ClientData));
                    OnLoginSuccess();
                }
                catch (InvalidAccountException e)
                {
                    State = ClientState.NotLoggedIn;
                    Log.Info($"{this} failed to login : Invalid ID {data.Name}");
                    SendAlert("로그인에 실패했습니다; 아이디 또는 비밀번호가 유효하지 않습니다.");
                    SendPacket(new S2CLoginPacket(S2CLoginPacket.LoginResult.InvalidName, ClientData));
                }
                catch (InvalidPasswordException e)
                {
                    State = ClientState.NotLoggedIn;
                    Log.Info($"{this} failed to login : Invalid Password {data.Name}");
                    SendAlert("로그인에 실패했습니다; 아이디 또는 비밀번호가 유효하지 않습니다.");
                    SendPacket(new S2CLoginPacket(S2CLoginPacket.LoginResult.InvalidPassword, ClientData));
                }
                catch (Exception e)
                {
                    State = ClientState.NotLoggedIn;
                    Log.Error($"{this} failed to login : Internal Error {data.Name} - {e.StackTrace}");
                    SendAlert("로그인에 실패했습니다; 내부 오류가 발생했습니다.");
                    SendPacket(new S2CLoginPacket(S2CLoginPacket.LoginResult.Error, ClientData));
                }
            });
        }
        private void Register(Packet packet)
        {
            if (State != ClientState.NotLoggedIn)
            {
                Log.Warn($"{this} tried to register when State {State.ToString()} != NotLoggedIn");
                return;
            }
            var data = packet.GetPacketData<C2SLoginPacket>();

            State = ClientState.LoggingIn;
            Type = ClientType.Registered;
            Log.Info($"{this} Trying Register with {data.Name} ...");
            Task.Run(async () =>
            {
                try
                {
                    var clientData = await YachuGameServer.Database.Register(data.Name, data.HashedPassword);
                    Name = data.Name;
                    Guid = clientData.guid;
                    State = ClientState.Connected;
                    YachuGameServer.Instance.ClientByGuid.TryAdd(Guid, this);
                    Log.Info($"Successfully Registered & Logged in {this}");
                    SendAlert($"어서오세요, {Name} 님!");
                    SendPacket(new S2CRegisterPacket(S2CRegisterPacket.RegisterResult.Success, ClientData));
                    OnLoginSuccess();
                }
                catch (DuplicatedNameException e)
                {
                    State = ClientState.NotLoggedIn;
                    Log.Info($"{this} failed to register : duplicated ID {data.Name}");
                    SendAlert("회원가입에 실패했습니다; 이미 존재하는 아이디입니다.");
                    SendPacket(new S2CRegisterPacket(S2CRegisterPacket.RegisterResult.DuplicatedName, ClientData));
                }
                catch (Exception e)
                {
                    State = ClientState.NotLoggedIn;
                    Log.Error($"{this} failed to register : Internal Error {data.Name} - {e.StackTrace}");
                    SendAlert("회원가입에 실패했습니다; 내부 오류가 발생했습니다.");
                    SendPacket(new S2CRegisterPacket(S2CRegisterPacket.RegisterResult.Error, ClientData));
                }
            });
        }

        private void OnLoginSuccess()
        {
            QueueUpdateUserData();
        }
        private void Logout(Packet packet)
        {
            if (State != ClientState.Connected)
            {
                Log.Warn($"{this} tried to logout when State {State.ToString()} != Connected");
                return;
            }
            var data = packet.GetPacketData<C2SLogoutPacket>();

            State = ClientState.NotLoggedIn;
            Name = string.Empty;
            YachuGameServer.Instance.ClientByGuid.TryRemove(Guid, out _);
            Guid = Guid.Empty;
        }

        public void QueueUpdateUserData() => Task.Run(UpdateUserData);
        /// <summary>
        /// 유저 정보를 DB에서 최신화합니다.
        /// </summary>
        public async Task UpdateUserData()
        {
            var userData = await YachuGameServer.Database.GetUserData(Guid);
            CachedUserData = userData;
            SyncUserDataWithClient();
        }

        public void QueueSaveUserData() => Task.Run(SaveUserData);
        /// <summary>
        /// 캐시된 유저 정보를 DB에 저장합니다.
        /// </summary>
        public async Task SaveUserData()
        {
            await YachuGameServer.Database.SetUserData(Guid, CachedUserData);
            SyncUserDataWithClient();
        }

        public void SyncUserDataWithClient()
        {
            SendPacket(new S2CUserDataUpdatePacket
            {
                ClientData = ClientData, 
                UserData = CachedUserData
            });
        }

        public void Tick() {
            ConnectionHandler.Tick();
            // 클라이언트가 HandShake를 일정 시간이상 보내지 않으면 접속 실패 처리
            if (State == ClientState.HandShaking) {
                var now = ExtraUtil.CurrentTimeInMillis;
                var elapsed = now - _acceptedTimeStamp;
                if (elapsed > 3 * 1000) {
                    Close();
                    return;
                }
            }
        }

        private static void RegisterPacketListeners() {
            PacketHandler<Client>.RegisterListeners(new List<PacketListener<Client>> {
                new (PacketType.Handshake, (packet, client) => client.HandShake(packet)),
                new (PacketType.Login, (packet, client) => client.Login(packet)),
                new (PacketType.Register, (packet, client) => client.Register(packet)),
                new (PacketType.Logout, (packet, client) => client.Logout(packet)),
                new (PacketType.UserDataUpdate, (packet, client) => client.QueueUpdateUserData()),
                new (PacketType.NameChange, (packet, client) => {

                    static void CancelResponse(Client client) {
                        Response(client, client.Name);
                    }
                    static void Response(Client client, string newName) {
                        client.SendPacket(new NameChangePacket { NewName = newName });
                    }
                    
                    var data = packet.GetPacketData<NameChangePacket>();

                    if (client.Guid == Guid.Empty)
                    {
                        Log.Warn($"{client} tried change name while GUID is Empty");
                        return;
                    }

                    var oldName = client.Name;
                    var newName = data.NewName;

                    // 방 참가 중에는 닉네임 변경을 막음
                    if (client.JoiningRoom != null) {
                        CancelResponse(client);
                        return;
                    }

                    // 닉네임 유효 검사
                    if (oldName == newName 
                        || newName.Length <= 0 
                        || newName.Length > Constants.MaxPlayerNameLength
                    ) {
                        CancelResponse(client);
                        return;
                    }

                    // 익명 게스트 계정은 그냥 허가
                    if (client.Type == ClientType.Anonymous)
                    {
                        client.Name = newName;
                        Response(client, newName);
                        client.SendAlert($"ID가 {newName} 으로 변경되었습니다.");
                        return;
                    }
                    // 로그인 계정은 DB 허락 필요
                    Task.Run(async () =>
                    {
                        try
                        {
                            var result = await YachuGameServer.Database.ChangeName(client, newName);
                            if (result)
                            {
                                // 허가
                                Log.Warn($"{client} changed name from {oldName} to {newName}");
                                client.Name = newName;
                                Response(client, newName);
                                client.SendAlert($"ID가 {newName} 으로 변경되었습니다.");
                            }
                        }
                        catch (MySqlException e)
                        {
                            if (e.Number == (int)MySqlErrorCode.DuplicateKeyEntry)
                            {
                                Log.Info($"{client} failed to change name by duplicated name {newName}");
                                CancelResponse(client);
                                client.SendAlert($"{newName}(은)는 다른 사람이 사용중인 ID입니다.");
                            }
                            else
                            {
                                Log.Warn($"{client} failed to change name : {e.Message}, {e.StackTrace}");
                                CancelResponse(client);
                                client.SendAlert($"내부 오류로 인해 ID 변경에 실패했습니다.");
                            }

                        }
                        catch (Exception e)
                        {
                            Log.Warn($"{client} failed to change name : {e.StackTrace}");
                            CancelResponse(client);
                            client.SendAlert($"내부 오류로 인해 ID 변경에 실패했습니다.");
                        }
                    });
                }),
                // 받은 데이터 길이가 0 => 연결 끊김 (실제로 오는 패킷은 아님)
                new (PacketType.UserClosed, (packet, client) => {
                    client.Close();
                }),
            });
        }
        
        public delegate void OnDisconnect(Client client);

        public OnDisconnect OnDisconnected { get; set; }

        
        
        public void Close() {
            State = ClientState.Disconnected;
            Log.Info($"Client Disconnected: {this}");
            OnDisconnected?.Invoke(this);
            ConnectionHandler.Close();

            ConnectionHandler = null;
            OnDisconnected = null;
        }

        public override string ToString() {
            if (IsDisconnected) {
                return "Unknown";
            }
            var joiner = new List<string>(3);
            joiner.Add(ConnectionHandler?.Socket?.RemoteEndPoint?.ToString() ?? "Unknown");
            if (Name?.Length > 0) {
                joiner.Add(Name);
            }
            if (Guid.Empty != Guid) {
                joiner.Add(Guid.ToString());
            }
            return string.Join("/", joiner);
            // return $"[{ConnectionHandler.Socket.RemoteEndPoint?.ToString() ?? "Unknown"}/] {Name}({Guid})";
        }
        public void SendPacket<T>(PacketData<T> packetData) where T : PacketData<T> {
            SendPacket(packetData.ToPacket());
        }

        public void SendPacket(Packet packet) {
            ConnectionHandler?.Send(packet);
        }

        public void SendAlert(string message) {
            var sendMessage
                = message.Length > Constants.MaxAlertMessageLength
                    ? message.Substring(0, Constants.MaxAlertMessageLength - 1)
                    : message;
            SendPacket(new S2CAlertPacket {Content = sendMessage});
        }
    }
}
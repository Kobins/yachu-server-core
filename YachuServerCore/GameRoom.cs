using System.Collections.Generic;
using System.Linq;
using Yachu.Server.Packets;
using Yachu.Server.Packets.Body;
using Yachu.Server.Util;

namespace Yachu.Server {
public class GameRoomPlayer {
    public Client Client { get; }
    public bool IsRoomHost { get; set; }
    public bool IsReady { get; set; }

    public GameRoomPlayer(Client client) {
        Client = client;
        Reset();
    }

    public void Reset() {
        IsReady = false;
        IsRoomHost = false;
    }
}

public class GameRoom {
    private static readonly List<GameRoom> PublicRoomList = new(Constants.MaxRoomCount);
    private static readonly List<GameRoom> WaitingRoomList = new(Constants.MaxRoomCount);

    public static void Initialize() {
        Log.Info("게임 방 초기화 ...");
        for (int i = 0; i < Constants.MaxRoomCount; i++) {
            var room = new GameRoom(i);
            PublicRoomList.Add(room);
        }

        RegisterPacketListeners();
        GamePlaySession.Initialize();
    }

    public RoomState State { get; private set; } = RoomState.Waiting;
    public int Number { get; }
    public string Name { get; set; }
    private GameRoomPlayer _roomHost = null;

    public GameRoomPlayer RoomHost {
        get => _roomHost;
        private set {
            if (_roomHost != null) _roomHost.IsRoomHost = false;
            _roomHost = value;
            if (value != null) value.IsRoomHost = true;
        }
    }

    public List<GameRoomPlayer> Players { get; } = new(Constants.MaxPlayerInRoom);

    public int MaxPlayer { get; }

    public int ArenaSizeIndex { get; set; } = 0;

    public GamePlaySession Session { get; private set; } = null;

    public override string ToString() {
        return $"[{Number}]{Name}({Players.Count}/{MaxPlayer})";
    }

    private ClientData[] ClientDataArray {
        get {
            var array = new ClientData[Constants.MaxPlayerInRoom];
            int i = 0;
            for (; i < Players.Count; ++i) {
                array[i] = Players[i].Client.ClientData;
            }

            for (; i < Constants.MaxPlayerInRoom; ++i) {
                array[i] = ClientData.Empty;
            }

            return array;
        }
    }
    private UserData[] UserDataArray {
        get {
            var array = new UserData[Constants.MaxPlayerInRoom];
            int i = 0;
            for (; i < Players.Count; ++i) {
                array[i] = Players[i].Client.CachedUserData;
            }

            for (; i < Constants.MaxPlayerInRoom; ++i) {
                array[i] = UserData.Empty;
            }

            return array;
        }
    }

    private bool[] PlayerReadyArray {
        get {
            var array = new bool[Constants.MaxPlayerInRoom];
            int i = 0;
            for (; i < Players.Count; ++i) {
                array[i] = Players[i].IsReady;
            }

            for (; i < Constants.MaxPlayerInRoom; ++i) {
                array[i] = false;
            }

            return array;
        }
    }

    public RoomData RoomData => new() {
        Clients = ClientDataArray,
        UserDatas = UserDataArray,
        Ready = PlayerReadyArray,
        number = Number,
        Name = Name,
        arenaSizeIndex = (short) ArenaSizeIndex,
        maxPlayer = (short) MaxPlayer,
        playerCount = (short) Players.Count,
    };

    public GameRoom(int number, int maxPlayer = Constants.DefaultPlayerCountInRoom) {
        Number = number;
        MaxPlayer = maxPlayer;
        Name = $"게임 방 {number}";
    }

    public void Reset() {
        EndGame();
        State = RoomState.Waiting;
        Name = $"게임 방 {Number}";
        ArenaSizeIndex = 0;
        Players.Clear();
    }

    public bool JoinClient(Client client) {
        if (Players.Count >= MaxPlayer) {
            Log.Error($"클라이언트 {client}가 꽉 찬 방 {this}에 중복 입장 시도");
            return false;
        }

        if (Players.FindIndex(it => it.Client == client) >= 0) {
            Log.Error($"클라이언트 {client}가 같은 방 {this}에 중복 입장 시도");
            return false;
        }

        if (client.JoiningRoom != null) {
            Log.Error($"클라이언트 {client}가 다른 방 {client.JoiningRoom}에 소속된 상태에서 또 다른 방 {this}에 입장 시도");
            return false;
        }

        var player = new GameRoomPlayer(client);
        // 방 초기화
        if (Players.Count <= 0) {
            Reset();
            WaitingRoomList.Add(this);
            RoomHost = player;
        }

        Players.Add(player);
        client.JoiningRoom = this;
        client.SendPacket(new S2CRoomEnterPacket {RoomData = RoomData});
        client.OnDisconnected += QuitClient;
        var packet = new S2CRoomNewUserPacket
        {
            NewPlayer = client.ClientData, 
            NewPlayerUserData = client.CachedUserData
        }.ToPacket();
        foreach (var otherClient in Players.Where(it => it.Client != client)) {
            otherClient.Client.SendPacket(packet);
        }

        Log.Info($"클라이언트 {client}가 방 {this}에 접속");
        return true;
    }

    public void QuitClient(Client client) {
        client.JoiningRoom = null;
        // 데디케이티드 서버인 경우 방 퇴장 패킷 전송
        if (Constants.DedicatedServer) {
            client.SendPacket(new S2CRoomExitPacket {RoomNumber = Number});
        }
        // 호스트 서버인 경우 바로 서버와 연결 끊음
        else {
            client.Close();
        }

        client.OnDisconnected -= QuitClient;
        var index = Players.FindIndex(it => it.Client == client);
        if (index == -1) {
            Log.Error($"클라이언트 {client}가 방 {this} 퇴장, 근데 플레이어 목록에 포함 안 됨");
        }
        else {
            var player = Players[index];
            // 방 퇴장 동기화
            var packet = new S2CRoomUserExitPacket {Index = (short) index}.ToPacket();
            foreach (var otherClient in Players.Where(it => it.Client != client)) {
                otherClient.Client.SendPacket(packet);
            }

            Players.Remove(player);
            Log.Info($"클라이언트 {client}가 방 {this}에서 퇴장");
            // 방장 승계
            if (RoomHost == player && Players.Count > 0) {
                RoomHost = Players[0];
            }
        }

        // 아무도 없어졌으면 그냥 방 초기화
        if (Players.Count <= 0) {
            Reset();
            WaitingRoomList.Remove(this);
        }
        // 직전에 꽉 찬 방이었다면
        else if (Players.Count + 1 == MaxPlayer && State == RoomState.Waiting) {
            WaitingRoomList.Add(this);
        }
    }

    public void StartGame() {
        if (State != RoomState.Waiting) {
            Log.Debug($"room.StartGame() cancelled by State != Waiting");
            return;
        }

        State = RoomState.Playing;
        WaitingRoomList.Remove(this);
        Session = new GamePlaySession(this);
    }

    public void EndGame() {
        if (Session != null) {
            Session.Dispose();
            Session = null;
        }

        State = RoomState.Waiting;
        // 레디 해제
        foreach (var player in Players) {
            player.IsReady = false;
        }
        var packet = new S2CRoomUpdatePacket {RoomData = RoomData};
        Players.ForEach(it => it.Client.SendPacket(packet));
    }


    public static GameRoom TryFindRoomOrNull() {
        if (WaitingRoomList.Count > 0) {
            var room = WaitingRoomList[0];
            WaitingRoomList.RemoveAt(0);
            return room;
        }

        foreach (var room in PublicRoomList) {
            if (room.State == RoomState.Waiting && room.Players.Count < room.MaxPlayer) {
                return room;
            }
        }

        return null;
    }

    private bool AllPlayerReady {
        get {
            for (int i = 1; i < Players.Count; i++) {
                if (!Players[i].IsReady) {
                    return false;
                }
            }

            return true;
        }
    }
    private static void RegisterPacketListeners() {
        PacketHandler<Client>.RegisterListeners(new List<PacketListener<Client>> {
            // 빠른 참가 버튼 눌렀을 때
            new(PacketType.RoomAutoEnter, (packet, client) => {
                Log.Info($"{client} sent RoomAutoEnter");
                if (client.JoiningRoom != null) {
                    Log.Error($"클라이언트 {client}가 방 {client.JoiningRoom}에 참여한 상태로 빠른 참가 시도");
                    return;
                }

                // 방 임의로 하나 찾아서 들어가기
                var room = GameRoom.TryFindRoomOrNull();
                if (room != null) {
                    room.JoinClient(client);
                }
                else {
                    Log.Error($"클라이언트 {client}가 들어갈 방을 찾지 못함");
                    client.SendAlert("지금은 빠른 참여가 불가능합니다.");
                }
            }),
            new(PacketType.RoomReady, (packet, client) => {
                var room = client.JoiningRoom;
                if (room == null) {
                    Log.Error($"클라이언트 {client}가 방에 참여하지 않은 상태로 ready 시도");
                    return;
                }

                var data = packet.GetPacketData<RoomReadyPacket>();
                var index = data.Index;
                if (index < 0 || index >= room.Players.Count || room.Players[index].Client != client) {
                    Log.Error($"클라이언트 {client}가 방 {room}에서 잘못된 index({index})로 ready 시도");
                    return;
                }

                var ready = data.Ready;
                room.Players[index].IsReady = ready;
                foreach (var player in room.Players) {
                    player.Client.SendPacket(packet);
                }
            }),
            /*
            new (PacketType.RoomArenaSizeUpdate, (packet, client) => {
                var room = client.JoiningRoom;
                if (room == null) {
                    Log.Error($"클라이언트 {client}가 방에 참여하지 않은 상태로 Arena Size 변경 시도");
                    return;
                }
                
                if (room.Clients.IndexOf(client) != 0) {
                    Log.Error($"클라이언트 {client}가 방장이 아닌 상태로 Arena Size 변경 시도");
                    return;
                }
                var data = packet.GetPacketData<RoomArenaSizePacket>();
                var index = data._arenaSizeIndex;
                if (index < 0 || index >= Constants.ArenaSizeDataList.Length) {
                    Log.Error($"클라이언트 {client}가 잘못된 Arena Size Index 보냄({index})");
                    return;
                }

                room.ArenaSizeIndex = index;
                foreach (var c in room.Clients) {
                    c.SendPacket(packet);
                }
            }),
            */
            // 게임 시작 버튼 눌렀을 때
            new(PacketType.RoomStart, (packet, client) => {
                Log.Debug($"RoomStart listened");
                var room = client.JoiningRoom;
                if (room == null) {
                    Log.Error($"클라이언트 {client}가 방에 참여하지 않은 상태로 게임 시작 시도");
                    return;
                }

                // 방장이 아니면
                if (room.RoomHost?.Client != client) {
                    Log.Error($"클라이언트 {client}가 방장이 아닌 상태로 게임 시작 시도");
                    return;
                }

                // 레디 안 한 사람 있다면
                if (!room.AllPlayerReady) {
                    Log.Info("ready info:");
                    for (int i = 0; i < room.Players.Count; i++) {
                        Log.Info($"[{i}]: {room.Players[i].IsReady}");
                    }
                    room.RoomHost?.Client.SendAlert("아직 준비하지 않은 사람이 있습니다.");
                    return;
                }


                Log.Debug($"called room.StartGame()");
                room.StartGame();
            }),
            // 클라이언트가 직접 RoomExit 타입의 패킷을 보냈을 때
            // -> 방을 제 발로 나가겠다는 의지
            new(PacketType.RoomExit, (packet, client) => {
                if (client.JoiningRoom == null) {
                    Log.Error($"클라이언트 {client}가 방에 참여하지 않은 상태로 퇴장 시도");
                    return;
                }

                client.JoiningRoom.QuitClient(client);
            }),
        });
    }
}
}
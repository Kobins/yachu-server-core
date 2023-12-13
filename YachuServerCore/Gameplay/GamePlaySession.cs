using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yachu.Server;
using Yachu.Server.Packets;
using Yachu.Server.Packets.Body;
using Yachu.Server.Util;

namespace Yachu.Server {
    public class GamePlayer {
        public Client Client { get; }
        public int Index { get; }
        public bool IsSceneLoaded { get; set; } = false;
        public bool IsTurnEnded { get; set; } = false;
        
        // -2 == 할당 안 됨, -1 == 무승부, 0 ~ Clients.Count-1 == 해당 index 플레이어 승리
        public const int GameEndFlagNotAssigned = -2;
        public const int GameEndFlagDraw = -1;
        public int GameEndWinner { get; set; } = GameEndFlagNotAssigned;
        public bool GameEndWinnerAssigned => GameEndWinner != GameEndFlagNotAssigned;
        
        public bool IsAlive { get; set; } = true;

        public GamePlayer(Client client, int index) {
            Client = client;
            Index = index;
        }
    }   
    
    /// <summary>
    /// 게임 플레이 중 관련된 정보를 담는 객체
    /// </summary>
    public class GamePlaySession {
        public static void Initialize() {
            RegisterPacketListeners();
        }

        private static GameRoom GetPlayingRoomOrNull(Client client) {
            var room = client.JoiningRoom;
            if(room == null || room.State != RoomState.Playing || room.Session == null) return null;
            return room;
        }

        private static void RegisterPacketListeners() {
            static void ForwardToSession(Packet packet, Client client) {
                GetPlayingRoomOrNull(client)?.Session.OnGamePacketReceived(packet, client);
            }
            PacketHandler<Client>.RegisterListeners(new List<PacketListener<Client>> {
                // 클라이언트에게서 씬 로드 완료 신호를 받았을 때
                new (PacketType.SceneLoadDone, (packet, client) => {
                    GetPlayingRoomOrNull(client)?.Session.OnSceneLoadDone(client);
                }),
                
                new (PacketType.GameCupUpdate, ForwardToSession),
                new (PacketType.GameDiceUpdate, ForwardToSession),
                new (PacketType.GameDiceThrow, ForwardToSession),
                new (PacketType.GameDiceDetermined, ForwardToSession),
                new (PacketType.GameSelect, ForwardToSession),
                new (PacketType.GameInteractCup, ForwardToSession),
                new (PacketType.GameHoldDice, ForwardToSession),
                new (PacketType.GameMarkScore, ForwardToSession),
                
                new (PacketType.GameTurnEnd, ForwardToSession),
                new (PacketType.GameEnd, ForwardToSession),
            });
        }

        public GameRoom Room { get; private set; }
        public List<Client> Clients { get; }
        public List<GamePlayer> Players { get; }
        public List<GamePlayer> OnlinePlayers => Players.FindAll(it => it.Client.IsConnected);
        private GamePlayState _state = GamePlayState.SceneLoading;

        public GamePlayState State {
            get => _state;
            private set {
                _state = value;
            }
        }

        public int GlobalTurn { get; private set; }
        public int CurrentTurn => GlobalTurn % Players.Count;
        public GamePlaySession(GameRoom room) {
            Log.Debug($"Created Play Session on room {room}");
            Room = room;
            Clients = new List<Client>(room.Players.ConvertAll(it => it.Client));
            Players = new List<GamePlayer>(Clients.Count);
            GlobalTurn = 0;
            int index = 0;
            foreach (var client in Clients) {
                var gamePlayClient = new GamePlayer(client, index);
                client.OnDisconnected += OnDisconnected;
                Players.Add(gamePlayClient);
                index++;
            }
            
            // 전체에게 게임 시작 패킷 보내기
            StartGame();
        }

        private void StartGame() {
            var now = ExtraUtil.CurrentTimeInMillis;
            var packet = new RoomStartPacket {
                Timestamp = now, 
            };
            Clients.SendPackets(packet);
        }
        private GameTurnStartPacket TurnStartPacket => new() {Timestamp = ExtraUtil.CurrentTimeInMillis, Turn = GlobalTurn};

        private void OnSceneLoadDone(Client client) {
            var index = Clients.IndexOf(client);
            if(index < 0) return;

            Players[index].IsSceneLoaded = true;

            // 모든 플레이어의 씬이 불러와졌으면
            if (Players.All(it => it.IsSceneLoaded)) {
                StartCurrentTurn();
            }
        }

        private void StartNextTurn() {
            ++GlobalTurn;
            var player = Players[CurrentTurn];
            // 죽거나 연결이 끊긴 플레이어는 자동으로 넘김
            while (!player.Client.IsConnected || !player.IsAlive) {
                ++GlobalTurn;
                player = Players[CurrentTurn];
            }
            StartCurrentTurn();
        }
        private void StartCurrentTurn() {
            State = GamePlayState.CupShaking;
            for (int i = 0; i < Players.Count; i++) {
                Players[i].IsTurnEnded = false;
            }
            // GameTurn 전송
            var packet = TurnStartPacket;
            Clients.SendPackets(packet);

            _timerStarted = packet.Timestamp;
            _diceThrow = 0;
        }

        private long _timerStarted;
        private int _diceThrow;

        private void OnGamePacketReceived(Packet packet, Client client) {
            var selfIndex = Clients.IndexOf(client);
            var player = Players[selfIndex];
            switch ((PacketType)packet.Type) {
                // 주사위&컵 위치 동기화
                case PacketType.GameDiceUpdate: 
                case PacketType.GameCupUpdate: {
                    if (CurrentTurn != selfIndex) { return; }
                    Broadcast(packet, selfIndex);
                    break;
                }
                // 컵 던짐
                case PacketType.GameDiceThrow: {
                    if (CurrentTurn != selfIndex) { return; }

                    State = GamePlayState.DiceThrowing;
                    Broadcast(packet, selfIndex);
                    break;
                }
                // 주사위 눈 결정됨
                case PacketType.GameDiceDetermined: {
                    if (CurrentTurn != selfIndex) { return; }

                    State = GamePlayState.Selecting;
                    Broadcast(packet, selfIndex);
                    break;
                }
                // 무언가 선택함
                case PacketType.GameSelect: {
                    if (CurrentTurn != selfIndex) { return; }

                    State = GamePlayState.Selecting;
                    Broadcast(packet, selfIndex);
                    break;
                }
                // 턴 종료 신호 
                case PacketType.GameTurnEnd: {
                    player.IsTurnEnded = true;
                    // 모든 클라이언트가 턴 끝남 신호 보냈으면 
                    if (OnlinePlayers.All(it => it.IsTurnEnded)) {
                        // 다음 턴 시작
                        StartNextTurn();
                    }
                    break;
                }
                // 게임 종료 신호
                case PacketType.GameEnd: {
                    var data = packet.GetPacketData<GameEndPacket>();
                    player.GameEndWinner = data.Index;
                    // 모든 플레이어가 게임 종료 신호를 보냄
                    if (OnlinePlayers.All(it => it.GameEndWinnerAssigned)) {
                        DetermineGameEnd();
                    }
                    break;
                }
                    
            }
        }
        private void Broadcast(Packet packet, int excludeIndex = -1) {
            for (int i = 0; i < Clients.Count; i++) {
                if(i == excludeIndex) continue;
                Clients[i].SendPacket(packet);
            }
        }

        /// <summary>
        /// 게임 종료 판정
        /// </summary>
        private void DetermineGameEnd() {
            State = GamePlayState.GameEnding;

            var onlinePlayers = OnlinePlayers;
            // 온라인인 플레이어가 없으면, 굳이 판정하지 않고 세션 닫아버림
            if (onlinePlayers.Count <= 0) {
                GameEnd(GamePlayer.GameEndFlagDraw);
                return;
            }
            var primaryWinner = onlinePlayers[0].GameEndWinner;
            // 전부 일치하는 결과를 내놓으면
            if (onlinePlayers.All(it => it.GameEndWinner == primaryWinner)) {
                // 게임 종료 패킷 전송
                GameEnd((short)primaryWinner);
            }
            // 일치하지 않는 결과를 내놓으면 (대참사)
            else {
                Log.Error($"방 {Room}의 게임 결과가 클라이언트마다 다름:");
                foreach (var player in onlinePlayers) {
                    Log.Error($" [{player.Index}, {player}] : {player.GameEndWinner}");
                }

                // 마지막에 턴이었던 사람의 승자 정보를 일단 전달함
                var lastTurnPlayer = Players[CurrentTurn];
                var winnerIndex = lastTurnPlayer.GameEndWinner;
                Log.Error($" - 마지막 턴 플레이어({lastTurnPlayer.Index}, {lastTurnPlayer.Client})의 판정 결과 {lastTurnPlayer.GameEndWinner}를 수용함");
                GameEnd((short)winnerIndex);
            }
        }

        private void GameEnd(short winnerIndex) {
            var winner = winnerIndex >= 0 ? Clients[winnerIndex] : null;
            if (winner != null)
            {
                var userData = winner.CachedUserData;
                userData.WinCount += 1; // 승자 승리 카운트 증가
                userData.Money += 100;
                winner.CachedUserData = userData;
            }
            var loser = winnerIndex >= 0 ? Clients[(winnerIndex + 1) % 2] : null; // TODO 2인플 하드코딩
            if (loser != null)
            {
                var userData = loser.CachedUserData;
                userData.LoseCount += 1; // 패자 패배 카운트 증가
                loser.CachedUserData = userData;
            }

            foreach (var c in Clients)
            {
                var userData = c.CachedUserData;
                userData.PlayCount += 1; // 게임 플레이 카운트 증가
                c.CachedUserData = userData;
                c.QueueSaveUserData(); // DB에 반영 및 클라와 동기화
            }
            var packet = new GameEndPacket { Index = winnerIndex, Client = winner?.ClientData ?? ClientData.Empty };
            Clients.SendPackets(packet);
            Room.EndGame();
        }

        
        /// <summary>
        /// 접속 종료 신호
        /// </summary>
        /// <param name="client">접속 종료한 클라이언트</param>
        private void OnDisconnected(Client client) {
            client.OnDisconnected -= OnDisconnected;
            if (Room == null) { // 이미 Disposed인 경우
                return;
            }
            var index = Clients.IndexOf(client);
            if(index < 0) return;

            var player = Players[index];
            // 접속 중인 플레이어
            var onlinePlayers = OnlinePlayers;
            if (onlinePlayers.Count <= 0) {
                // 어차피 이 시점에서는 방이 해체됨
                return;
            }
            
            if (onlinePlayers.Count <= 1) {
                var winnerPlayer = onlinePlayers[0];
                GameEnd((short)winnerPlayer.Index);
                return;
            }

            // 현재 턴인 플레이어가 나간 경우
            if (CurrentTurn == index) {
                // 강제 턴 종료
                StartNextTurn();
            }

        }

        public void Dispose() {
            Room = null;
            foreach (var client in Clients) {
                client.OnDisconnected -= OnDisconnected;
            }
            Players.Clear();
        }
    }
}
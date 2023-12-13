using System.Runtime.InteropServices;

namespace Yachu.Server.Util {
    public static class Constants {
        public static bool DedicatedServer = true;
        // 서버 관련 상수
        public const int ProtocolVersion = 4;
        public const int DefaultPort = 10020;
        public static int MaxConnection => DedicatedServer ? 64 : MaxPlayerInRoom;
        public const int SocketBufferSize = 1024;
        public const int TickPerSeconds = 30;
        public const float TickInSeconds = 1.0f / TickPerSeconds;
        public const int TickInMilliseconds = (int)(TickInSeconds * 1000);
        public const int MinimumPlayablePlayerCountInRoom = 2;
        public const int DefaultPlayerCountInRoom = MinimumPlayablePlayerCountInRoom;
        public const int MaxPlayerInRoom = 8;

        // 패킷 관련 상수
        public const CharSet DefaultCharSet = CharSet.Unicode;
        public const int MaxAlertMessageLength = 128;
        public const int MaxPlayerNameLength = 32;
        public const int SHA2Length = 64;
        public static int MaxRoomCount => DedicatedServer ? (MaxConnection/2) : 1;
        public const int MaxRoomNameLength = 40;

        public const int MaxItemNameLength = 64;

        public const int SubtotalBonusScore = 63;
        public const int ItemViewPageCapacity = 12;
        public const float TimeLimitInTurn = 30f;

    }
}
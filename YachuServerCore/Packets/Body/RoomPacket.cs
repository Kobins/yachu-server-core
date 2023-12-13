using System;
using System.Runtime.InteropServices;
using Yachu.Server.Util;

namespace Yachu.Server.Packets.Body {
[Serializable]
public enum RoomState {
    Waiting,
    Playing,
}

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public struct RoomData {
    public int number;
    public short maxPlayer;
    public short playerCount;
    public short arenaSizeIndex;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.MaxRoomNameLength)]
    public string Name;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Constants.MaxPlayerInRoom)]
    public ClientData[] Clients;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Constants.MaxPlayerInRoom)]
    public bool[] Ready;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Constants.MaxPlayerInRoom)]
    public UserData[] UserDatas;
}

/// <summary>
/// 빠른 매칭 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class C2SRoomAutoEnter : PacketData<C2SRoomAutoEnter> {
    public C2SRoomAutoEnter() : base(PacketType.RoomAutoEnter) {
    }
}
/// <summary>
/// 방 만들기 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class C2SRoomCreate : PacketData<C2SRoomCreate> {
    public C2SRoomCreate() : base(PacketType.RoomCreate) {
    }
    
}

/// <summary>
/// 방에 들어갈 때 자신 포함한 방 정보 보냄
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class S2CRoomEnterPacket : PacketData<S2CRoomEnterPacket> {
    public S2CRoomEnterPacket() : base(PacketType.RoomEnter) {
    }

    public RoomData RoomData;
}

/// <summary>
/// 방에 들어갈 때 자신 포함한 방 정보 보냄
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class S2CRoomUpdatePacket : PacketData<S2CRoomUpdatePacket> {
    public S2CRoomUpdatePacket() : base(PacketType.RoomUpdate) {
    }

    public RoomData RoomData;
}

/// <summary>
/// 새 유저가 들어왔을 때 방의 유저에게 보냄
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class S2CRoomNewUserPacket : PacketData<S2CRoomNewUserPacket> {
    public S2CRoomNewUserPacket() : base(PacketType.RoomNewUser) {
    }

    /// <summary>
    /// 새로 들어온 유저. List에 삽입은 클라이언트가 판단
    /// </summary>
    public ClientData NewPlayer;
    public UserData NewPlayerUserData;
}

/// <summary>
/// 방에서 나갈 때 보냄, 자의가 아닐 수도 있음
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class S2CRoomExitPacket : PacketData<S2CRoomExitPacket> {
    public S2CRoomExitPacket() : base(PacketType.RoomExit) {
    }

    /// <summary>
    /// 나간 방 번호
    /// </summary>
    public int RoomNumber;
    // TODO 나간 이유 추가? (직접 나감, 강제 퇴장 등)
}

/// <summary>
/// 다른 유저가 퇴장했을 때, 서버->클라이언트가 알림
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class S2CRoomUserExitPacket : PacketData<S2CRoomUserExitPacket> {
    public S2CRoomUserExitPacket() : base(PacketType.RoomExitUser) {
    }

    /// <summary>
    /// 나간 유저의 List 상 index
    /// </summary>
    public short Index;
}

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public struct ArenaSizeData {
    public ushort ArenaWidth, ArenaHeight;

    public readonly int SizeInBits => ArenaWidth * ArenaHeight;
    public readonly int SizeInBytes => (int) (SizeInBits / 8.0);

    public readonly int SizeInBytesAllocation {
        get {
            if (SizeInBits % 8 == 0) {
                return SizeInBytes;
            }

            return SizeInBytes + 1;
        }
    }
}

public struct ArenaSizeDataWithName {
    public string ArenaName;
    public ArenaSizeData ArenaSizeData;
}

/// <summary>
/// 게임 시작 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class RoomStartPacket : PacketData<RoomStartPacket> {
    public RoomStartPacket() : base(PacketType.RoomStart) {
    }

    public long Timestamp;

    /*
    public ArenaSizeData _arenaSize;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Constants.MaxPlayerInRoom)]
    public int[] _spawnPointsX;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Constants.MaxArenaSizeInBytes)] // 약 4KB
    public byte[] _arenaData;
    */
}

/// <summary>
/// 준비 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class RoomReadyPacket : PacketData<RoomReadyPacket> {
    public RoomReadyPacket() : base(PacketType.RoomReady) {
    }
    /// <summary>
    /// 변경된 레디 상태
    /// </summary>
    public bool Ready;
    /// <summary>
    /// 서버 -> 클라이언트일 경우 레디한 플레이어 index
    /// </summary>
    public int Index;

}
}
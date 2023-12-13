using System;
using System.Runtime.InteropServices;
using Yachu.Server.Util;

namespace Yachu.Server.Packets.Body {
public enum GamePlayState {
    /// <summary>
    /// 씬 로드 중
    /// </summary>
    SceneLoading,

    /// <summary>
    /// 인트로
    /// </summary>
    Intro,

    /// <summary>
    /// 컵 흔드는 중
    /// </summary>
    CupShaking,

    /// <summary>
    /// 주사위 굴러가는 중
    /// </summary>
    DiceThrowing,

    /// <summary>
    /// 점수판 선택 & 주사위 고르는 중
    /// </summary>
    Selecting,

    /// <summary>
    /// 점수 선택하여 턴 끝나는 중
    /// </summary>
    TurnEnding,

    /// <summary>
    /// 게임 종료 중
    /// </summary>
    GameEnding,
}

/// <summary>
/// 턴 시작 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class GameTurnStartPacket : PacketData<GameTurnStartPacket> {
    public GameTurnStartPacket() : base(PacketType.GameTurnStart) {
    }

    public long Timestamp;
    public int Turn;
}


/// <summary>
/// 컵 위치 동기화 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class GameCupUpdatePacket : PacketData<GameCupUpdatePacket> {
    public GameCupUpdatePacket() : base(PacketType.GameCupUpdate) {
    }

    public DiceTransform CupTransform;
}

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public struct DiceTransform {
    // 위치 (6바이트(48비트, 16/16/16)로 양자화됨, [-8, 8] 범위 표현 가능)
    public short x, y, z;
    private const float MinimumPosition = -8f;
    private const float MaximumPosition = 8f;
    private static float Clamp(float f, float min = MinimumPosition, float max = MaximumPosition) => Math.Min(max, Math.Max(min, f));
    public void SetPosition(float x, float y, float z) {
        this.x = (short) (Clamp(x) * 4096f);
        this.y = (short) (Clamp(y) * 4096f);
        this.z = (short) (Clamp(z) * 4096f);
    }

    public Tuple<float, float, float> GetPosition() {
        return new Tuple<float, float, float>(
            x / 4096f,
            y / 4096f,
            z / 4096f
        );
    }

    /* // 사원수 그대로 사용
    public float rx, ry, rz, rw;
    public void SetRotation(float[] normalizedQuaternion) {
        rx = normalizedQuaternion[0];
        ry = normalizedQuaternion[1];
        rz = normalizedQuaternion[2];
        rw = normalizedQuaternion[3];
    }

    public void GetRotation(out float[] q) {
        q = new float[4];
        q[0] = rx;
        q[1] = ry;
        q[2] = rz;
        q[3] = rw;
    }
    */
     // 양자화 하려 했는데 뭔가 정확하지가 않아서 보류

    // 회전 (4바이트(32비트, 2/10/10/10)로 양자화됨)
    // https://silken-magpie-efe.notion.site/Quaternion-Quantization-f9c9e75f13fa4ad9841a6750c7c13ccd
    // https://github.com/jpreiss/quatcompress/blob/master/quatcompress.h
    public uint rotation;

    // private static readonly float _Sqrt1_2 = (1.0f) / (float)Math.Sqrt(2.0); 
    private static readonly float _Sqrt1_2 = 0.707106781186547524401f; // https://docs.microsoft.com/ko-kr/cpp/c-runtime-library/math-constants?view=msvc-170
    public void SetRotation(float[] normalizedQuaternion) {
        uint largestIndex = 0;
        for (uint i = 1; i < 4; ++i) {
            if (Math.Abs(normalizedQuaternion[i]) > Math.Abs(normalizedQuaternion[largestIndex])) {
                largestIndex = i;
            }
        }

        uint negate = (uint)(normalizedQuaternion[largestIndex] < 0 ? 1 : 0);
        rotation = largestIndex;
        for (int i = 0; i < 4; ++i) {
            if (i != largestIndex) {
                uint negativeBit = (uint)(normalizedQuaternion[i] < 0 ? 1 : 0) ^ negate;
                uint mag = (uint)(((1 << 9) - 1) * (Math.Abs(normalizedQuaternion[i]) / _Sqrt1_2) + 0.5f);
                rotation = (rotation << 10) | (negativeBit << 9) | mag;
            }
        }
    }

    public void GetRotation(out float[] q) {
        q = new float[4];
        const uint mask = (1 << 9) - 1;

        uint comp = rotation;
        uint largestIndex = comp >> 30;
        float sumOfSquares = 0;
        for (int i = 3; i >= 0; --i) {
            if (i != largestIndex) {
                uint mag = comp & mask;
                uint negativeBit = (comp >> 9) & 0x1;
                comp >>= 10;
                q[i] = _Sqrt1_2 * ((float) mag) / mask;
                if (negativeBit == 1) {
                    q[i] = -q[i];
                }

                sumOfSquares += q[i] * q[i];
            }
        }

        q[largestIndex] = (float)Math.Sqrt(1.0f - sumOfSquares);
    }
    
}

public enum DiceHitSoundType : byte {
    Weak = 0,
    Hard,
    Hardest,
    TypeCount,
}

public enum DiceHitMaterialType : byte {
    Dice = 0,
    Cup,
    BoardFloor,
    BoardWall,
    TypeCount,
}

/// <summary>
/// 주사위 위치 동기화 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class GameDiceUpdatePacket : PacketData<GameDiceUpdatePacket> {
    public GameDiceUpdatePacket() : base(PacketType.GameDiceUpdate) {
    }

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
    public byte[] Sounds;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
    public DiceTransform[] Transforms;

    private const byte SoundTypeCount = (byte)DiceHitSoundType.TypeCount;
    private const byte MaterialTypeCount = (byte)DiceHitMaterialType.TypeCount;
    private const byte SoundTypeSize = 2;
    private const byte SoundTypeMask = (1 << SoundTypeSize) - 1;
    private const byte EmptySound = SoundTypeCount | (MaterialTypeCount << SoundTypeSize); 
    public void SetSound(int index, DiceHitSoundType soundType, DiceHitMaterialType materialType) {
        if(index < 0 || index >= 5) return;
        Sounds[index] = (byte)soundType;
        Sounds[index] |= (byte)((byte)materialType << SoundTypeSize);
    }

    public void ClearSound(int index) {
        Sounds[index] = EmptySound;
    }

    public bool GetSound(int index, out DiceHitSoundType soundType, out DiceHitMaterialType materialType) {
        if (index < 0 || index >= 5) {
            soundType = DiceHitSoundType.TypeCount;
            materialType = DiceHitMaterialType.TypeCount;
            return false;
        }
        var soundTypeRaw = Sounds[index] & SoundTypeMask;
        var materialTypeRaw = (Sounds[index] >> SoundTypeSize);
        if (soundTypeRaw >= SoundTypeCount || materialTypeRaw >= MaterialTypeCount) {
            soundType = DiceHitSoundType.TypeCount;
            materialType = DiceHitMaterialType.TypeCount;
            return false;
        }

        soundType = (DiceHitSoundType) soundTypeRaw;
        materialType = (DiceHitMaterialType) materialTypeRaw;
        return true;
    }   
    
}
/// <summary>
/// 주사위 던짐 이벤트 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class GameDiceThrowPacket : PacketData<GameDiceThrowPacket> {
    public GameDiceThrowPacket() : base(PacketType.GameDiceThrow) {
    }
}
/// <summary>
/// 주사위 눈 결정 이벤트 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class GameDiceDeterminedPacket : PacketData<GameDiceDeterminedPacket> {
    public GameDiceDeterminedPacket() : base(PacketType.GameDiceDetermined) {
    }

    public long Timestamp;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
    public byte[] numbers;
}

/// <summary>
/// 마우스 호버링 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class GameSelectPacket : PacketData<GameSelectPacket> {
    [Serializable]
    public enum SelectType : byte {
        ScoreBoard,
        Dice,
        Cup,
    }
    public GameSelectPacket() : base(PacketType.GameSelect) {
    }

    public SelectType Selection;
    /// <summary>
    /// 추가 데이터
    /// ScoreBoard -> 점수 종류 index(1바이트) & 점수(3바이트)
    /// Dice -> interact false 일 경우 주사위 index
    /// Dice -> interact true일 경우 3비트씩 끊어서 5개 keep 상태 공유
    /// Cup -> 미사용
    /// </summary>
    public uint Data = 0;
    /// <summary>
    /// 영향 여부, false 시 그냥 마우스 올리기만, true 시 실제로 해당 기능 작동함
    /// </summary>
    public bool Interact = false;


    private const int ScoreIndexSizeInBits = 8;
    private const int ScoreIndexMask = (1 << ScoreIndexSizeInBits) - 1;

    public GameSelectPacket SetScoreboard(bool mark, int index, int score = 0) {
        Data = (uint) index;
        Data |= (uint)(score << ScoreIndexSizeInBits);
        Selection = SelectType.ScoreBoard;
        Interact = mark;
        return this;
    }
    public bool TryGetScoreBoard(out bool mark, out uint index, out uint score) {
        mark = Interact;
        if (Selection != SelectType.ScoreBoard) {
            score = 0;
            index = 0;
            return false;
        }
        index = Data & ScoreIndexMask;
        score = Data >> ScoreIndexSizeInBits;
        return true;
    }

    private const byte DiceKeepIndexSize = 3;
    private const byte DiceKeepIndexMask = (1 << DiceKeepIndexSize) - 1;
    private const uint DiceKeepIndexInvalid = DiceKeepIndexMask;

    public GameSelectPacket SetDiceKeep(in int[] keepIndexes) {
        Selection = SelectType.Dice;
        Interact = true;
        Data = 0;
        for (int i = 0; i < 5; ++i) {
            Data <<= DiceKeepIndexSize;
            var keepIndex = keepIndexes[i];
            Data |= keepIndex >= 0 && keepIndex < 5 ? (uint)keepIndex : DiceKeepIndexInvalid;
        }

        return this;
    }
    
    public GameSelectPacket SetDiceSelect(int index) {
        Selection = SelectType.Dice;
        Interact = false;
        Data = (uint) index;

        return this;
    }

    public bool TryGetDice(out bool interact, out int[] keepIndexesOrSelectIndex) {
        if (Selection != SelectType.Dice) {
            keepIndexesOrSelectIndex = null;
            interact = false;
            return false;
        }

        var comp = Data;
        interact = this.Interact;
        if (interact) {
            keepIndexesOrSelectIndex = new int[5];
            for (int i = 4; i >= 0; --i) {
                var keepIndex = comp & (DiceKeepIndexMask);
                keepIndexesOrSelectIndex[i] = keepIndex < 5 ? (int) keepIndex : -1;
                comp >>= DiceKeepIndexSize;
            }
        }
        else {
            keepIndexesOrSelectIndex = new int[1] {(int)Data};
        }

        return true;

    }

    public const byte CupStartShakeFlag = 0;
    public const byte CupCancelShakeFlag = byte.MaxValue;
    
    public bool IsStartShake() {
        return Selection == SelectType.Cup && Interact && Data == CupStartShakeFlag;
    }
    public bool IsCancelShake() {
        return Selection == SelectType.Cup && Interact && Data == CupCancelShakeFlag;
    }

    public GameSelectPacket SetCup(bool interact, bool startShake = false) {
        Selection = SelectType.Cup;
        Interact = interact;
        Data = !interact ? (byte) 0 : (startShake ? CupStartShakeFlag : CupCancelShakeFlag);
        return this;
    }

}

/// <summary>
/// 턴 종료 판정 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class GameTurnEndPacket : PacketData<GameTurnEndPacket> {
    public GameTurnEndPacket() : base(PacketType.GameTurnEnd) {
    }
}


/// <summary>
/// 게임 종료 판정 패킷
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
public class GameEndPacket : PacketData<GameEndPacket> {
    public GameEndPacket() : base(PacketType.GameEnd) {
    }

    /// <summary>
    /// 승리자, 음수일 경우 무승부 판정
    /// </summary>
    public short Index;

    public ClientData Client;
}
}
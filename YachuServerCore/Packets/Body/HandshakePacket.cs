using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Yachu.Server.Util;

namespace Yachu.Server.Packets.Body {
    
    /// <summary>
    /// 클라이언트 정보 보냄, DB 구축되면 바뀔 수도...
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public class S2CHandshakePacket : PacketData<S2CHandshakePacket> {
        public S2CHandshakePacket() : base(PacketType.Handshake) {}
    }
    /// <summary>
    /// 클라이언트로부터 정보 받기
    /// 플레이어 정보 식별
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public class C2SHandshakePacket : PacketData<C2SHandshakePacket> {
        public C2SHandshakePacket() : base(PacketType.Handshake) {}
        public int Version = Constants.ProtocolVersion;
    }
    
    /// <summary>
    /// 로그인 요청
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public class C2SLoginPacket : PacketData<C2SLoginPacket> {
        public C2SLoginPacket() : base(PacketType.Login) {}
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.MaxPlayerNameLength)]
        public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.SHA2Length)]
        public string HashedPassword;

        public C2SLoginPacket(string name, string rawPassword) : base(PacketType.Login)
        {
            Name = name;
            HashedPassword = ExtraUtil.HashRawPassword(rawPassword);
        }
    }
    
    /// <summary>
    /// 로그인 응답
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public class S2CLoginPacket : PacketData<S2CLoginPacket> {
        public S2CLoginPacket() : base(PacketType.Login) {}

        public enum LoginResult
        {
            /// <summary>
            /// 로그인 성공
            /// </summary>
            Success,
            /// <summary>
            /// 해당하는 계정 없음
            /// </summary>
            InvalidName,
            /// <summary>
            /// 계정 비밀번호 불일치
            /// </summary>
            InvalidPassword,
            /// <summary>
            /// 이미 로그인된 계정
            /// </summary>
            AlreadyLoggedOn,
            /// <summary>
            /// 알 수 없는 내부 오류
            /// </summary>
            Error,
        }

        public LoginResult Result;
        public ClientData ClientData;

        public S2CLoginPacket(LoginResult result) : this(result, ClientData.Empty) { }
        public S2CLoginPacket(LoginResult result, ClientData clientData) : base(PacketType.Login)
        {
            Result = result;
            ClientData = clientData;
        }
    }
    
    /// <summary>
    /// 회원가입 요청
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public class C2SRegisterPacket : PacketData<C2SRegisterPacket> {
        public C2SRegisterPacket() : base(PacketType.Register) {}
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.MaxPlayerNameLength)]
        public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.SHA2Length)]
        public string HashedPassword;

        public C2SRegisterPacket(string name, string rawPassword) : base(PacketType.Register)
        {
            Name = name;
            HashedPassword = ExtraUtil.HashRawPassword(rawPassword);
        }
    }
    
    /// <summary>
    /// 회원가입 응답
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public class S2CRegisterPacket : PacketData<S2CRegisterPacket> {
        public S2CRegisterPacket() : base(PacketType.Register) {}

        public enum RegisterResult
        {
            /// <summary>
            /// 로그인 성공
            /// </summary>
            Success,
            /// <summary>
            /// 중복 아이디
            /// </summary>
            DuplicatedName,
            /// <summary>
            /// 알 수 없는 내부 오류
            /// </summary>
            Error,
        }

        public RegisterResult Result;
        public ClientData ClientData;

        public S2CRegisterPacket(RegisterResult result) : this(result, ClientData.Empty) { }
        public S2CRegisterPacket(RegisterResult result, ClientData clientData) : base(PacketType.Register)
        {
            Result = result;
            ClientData = clientData;
        }
    }
    
    /// <summary>
    /// 로그아웃
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public class C2SLogoutPacket : PacketData<C2SLogoutPacket> {
        public C2SLogoutPacket() : base(PacketType.Logout) {}
    }
    
}
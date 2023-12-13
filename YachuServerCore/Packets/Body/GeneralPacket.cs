using System;
using System.Runtime.InteropServices;
using Yachu.Server.Util;

namespace Yachu.Server.Packets.Body {
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public class S2CAlertPacket : PacketData<S2CAlertPacket> {
        public S2CAlertPacket() : base(PacketType.AlertMessage) {}

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.MaxAlertMessageLength)]
        public string Content;
    }
    
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public class NameChangePacket : PacketData<NameChangePacket> {
        public NameChangePacket() : base(PacketType.NameChange) {}

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.MaxPlayerNameLength)]
        public string NewName;
    }
}
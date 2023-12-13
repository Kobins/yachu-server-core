using System;
using System.Runtime.InteropServices;
using Yachu.Server.Util;

namespace Yachu.Server.Packets.Body
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = Constants.DefaultCharSet)]
    public class S2CUserDataUpdatePacket : PacketData<S2CUserDataUpdatePacket> {
        public S2CUserDataUpdatePacket() : base(PacketType.UserDataUpdate) {
        }
        public ClientData ClientData;
        public UserData UserData;
    }
}
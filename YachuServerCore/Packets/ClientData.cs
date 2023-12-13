using System;
using System.Runtime.InteropServices;
using Yachu.Server.Util;

namespace Yachu.Server.Packets {
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public struct ClientData {
        public static readonly ClientData Empty = new() {
            guid = Guid.Empty,
            name = "Empty",
            registered = false
        };
        public Guid guid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.MaxPlayerNameLength)]
        public string name;
        [MarshalAs(UnmanagedType.Bool)]
        public bool registered;
    }
}
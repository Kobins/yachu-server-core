using System;
using System.Runtime.InteropServices;
using Yachu.Server.Database;
using Yachu.Server.Util;

namespace Yachu.Server.Packets
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public struct UserData
    {
        public static readonly UserData Empty = new()
        {
            Money = 0,
            PlayCount = 0,
            WinCount = 0,
            LoseCount = 0,
        };
        
        public int Money;
        public int PlayCount;
        public int WinCount;
        public int LoseCount;
        

// 230619 : 일정 상 유기됨
        /*
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)ItemType.TypeCount)]
        public int[] EquipmentID;
        */
    }
}
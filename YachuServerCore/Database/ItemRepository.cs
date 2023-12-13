using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Yachu.Server.Util;

// 230619 : 일정 상 유기됨

namespace Yachu.Server.Database
{ 
    
    public enum ItemType : int {
        DiceSkin = 0, // 주사위 스킨 
        TypeCount,
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public class Item
    {
        public int Id;
        public ItemType Type;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.MaxItemNameLength)]
        public string Name;
    }
    public class ItemRepository
    {
        public Dictionary<int, Item> ItemById { get; } = new();
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet = Constants.DefaultCharSet)]
    public class UserItem
    {
        public Item Item;
        public DateTime PurchasedDate;
        public DateTime ExpireDate;
    }
    // TODO 유저별 아이템 종류, 아이템, 구매시간, 소지 만료기한 전체 보관
    /*
    public class UserItemRepository
    {
        public Dictionary<ItemType>
    }
    */
}
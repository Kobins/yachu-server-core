using System;

namespace Yachu.Server.Packets {
    public enum PacketType : ushort {
        Handshake = 1,
        Login,
        Register,
        Logout,
        
        AlertMessage,
        NameChange,
        UserDataUpdate, // 돈, 승패수, 아이템 장착 상태 요청 및 응답
        PurchaseItem, // 재화로 아이템 구매 요청 및 응답

        RoomAutoEnter,
        RoomCreate,
        RoomEnter,
        RoomUpdate,
        RoomNewUser,
        RoomExit,
        RoomExitUser,
        RoomReady,
        RoomStart,
        
        SceneLoadDone,

        GameTurnStart,
        
        GameCupUpdate, // 컵 위치 동기화
        GameDiceUpdate, // 주사위 위치 동기화
        GameDiceThrow, // 주사위 던짐 이벤트
        GameDiceDetermined, // 주사위 
        GameSelect, // Selecting 중 점수판, 주사위, 컵 등 뭐든 마우스에 올리던, 키보드로 선택하던 함
        GameInteractCup, // 컵 선택으로 CupShaking으로 전환
        GameHoldDice, // 특정 주사위를 홀드 또는 홀드 해제
        GameMarkScore, // 점수 마킹
        
        GameTurnEnd,
        GameEnd,

        UserClosed,
        
        PacketCount
    }
}
using System;
using System.Threading.Tasks;
using Yachu.Server.Packets;

namespace Yachu.Server.Database
{
    /// <summary>
    /// 로그인 시 해당하는 계정 이름이 없을 때 발생
    /// </summary>
    public class InvalidAccountException : Exception {
        public InvalidAccountException(string message) : base(message) { }
    }
    /// <summary>
    /// 로그인 시 해당하는 계정에 대한 비밀번호가 일치하지 않을 때 발생
    /// </summary>
    public class InvalidPasswordException : Exception {
        public InvalidPasswordException(string message) : base(message) { }
    }
    /// <summary>
    /// 회원가입 시 계정 이름이 중복될 때 발생 
    /// </summary>
    public class DuplicatedNameException : Exception {
        public DuplicatedNameException(string message) : base(message) { }
    }
    public interface IDatabaseAdapter
    {
        
        /// <summary>
        /// DB에 로그인 쿼리를 보내 해당하는 GUID를 얻어옵니다.
        /// </summary>
        /// <param name="name">계정 이름입니다.</param>
        /// <param name="hashedPassword">계정의 SHA-256 해시된 비밀번호입니다.</param>
        /// <returns></returns>
        /// <exception cref="InvalidAccountException">해당하는 계정 이름 또는 비밀번호가 유효하지 않을 경우 호출</exception>
        public Task<ClientData> Login(string name, string hashedPassword);

        /// <summary>
        /// DB에 회원가입 쿼리를 보내고, 새로 생성된 계정 정보에 해당하는 GUID를 얻어옵니다.
        /// </summary>
        /// <param name="name">계정 이름입니다.</param>
        /// <param name="hashedPassword">계정의 SHA-256 해시된 비밀번호입니다.</param>
        /// <returns></returns>
        public Task<ClientData> Register(string name, string hashedPassword);

        /// <summary>
        /// DB에 아이디 변경 요청을 보냅니다.
        /// </summary>
        /// <param name="client">해당하는 클라이언트입니다.</param>
        /// <param name="newName">변경할 아이디입니다.</param>
        /// <returns>true일 시 성공, false일 시 모종의 이유로 실패</returns>
        public Task<bool> ChangeName(Client client, string newName);

        /// <summary>
        /// DB에 유저 데이터를 요청합니다. 
        /// </summary>
        /// <param name="guid">해당하는 유저 Guid입니다.</param>
        /// <returns>승패기록, 재화, 아이템 장착 상황에 해당하는 UserData를 가져옵니다.</returns>
        public Task<UserData> GetUserData(Guid guid);

        /// <summary>
        /// DB에 유저 데이터를 저장합니다.
        /// </summary>
        /// <param name="guid">해당하는 유저 Guid입니다.</param>
        /// <param name="userData">해당하는 UserData입니다.</param>
        public Task SetUserData(Guid guid, UserData userData);

// 230619 : 일정 상 유기됨
        /*
        /// <summary>
        /// DB 아이템 테이블을 갱신합니다.
        /// </summary>
        /// <param name="repository"></param>
        /// <returns></returns>
        public Task UpdateItemRepository(ItemRepository repository);

        /// <summary>
        /// DB에서 해당하는 아이템을 가지고 있는지 검사합니다.
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="itemId"></param>
        /// <returns></returns>
        public Task<bool> HasValidPurchase(Guid guid, int itemId);
        */
    }
}
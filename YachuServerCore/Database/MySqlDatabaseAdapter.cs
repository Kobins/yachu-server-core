using System;
using System.Configuration;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Yachu.Server.Packets;
using Yachu.Server.Util;

namespace Yachu.Server.Database
{
    public static class MySqlExtensions
    {
        public static MySqlCommand NewCommand(this MySqlConnection connection, string query) 
            => new(query, connection);
        public static MySqlCommand NewCommand(this MySqlConnection connection, MySqlTransaction transaction, string query) 
            => new(query, connection, transaction);
    }
    public class MySqlDatabaseAdapter : IDatabaseAdapter
    {
        private static string dbHost = "localhost";
        private static string dbPort = "3306";
        private static string dbName = "yachu";
        private static string dbUserId = "root";
        private static string dbPassword = "root";
        private static string dbConnectionAddress = 
            $"Server={dbHost};Port={dbPort};Database={dbName};Uid={dbUserId};Pwd={dbPassword};";

        private static object _instanceLock = new();
        private static MySqlDatabaseAdapter _instance;
        public static MySqlDatabaseAdapter Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        _instance = new MySqlDatabaseAdapter();
                    }
                }
                return _instance;
            }
        }

        private static MySqlConnection NewConnection() => new MySqlConnection(dbConnectionAddress);
        
        private MySqlDatabaseAdapter()
        {
            var rawPort = ConfigurationManager.AppSettings["MySQL.Port"] ?? dbPort;
            if (!uint.TryParse(rawPort, out uint port))
            {
                Log.Error($"{rawPort}는 알 수 없는 종류의 포트번호입니다.");
                return;
            }
            var builder = new MySqlConnectionStringBuilder
            {
                Server = ConfigurationManager.AppSettings["MySQL.Host"] ?? dbHost,
                Port = port,
                Database = ConfigurationManager.AppSettings["MySQL.Name"] ?? dbName,
                UserID = ConfigurationManager.AppSettings["MySQL.UserId"] ?? dbUserId,
                Password = ConfigurationManager.AppSettings["MySQL.Password"] ?? dbPassword
            };
            dbConnectionAddress = builder.ToString();
            Log.Info($"MySQL 연결 코드: {dbConnectionAddress}");
            
            using var connection = NewConnection();
            connection.Open();
            if (!connection.Ping())
            {
                Log.Error("DB 연결에 실패했습니다.");
                throw new Exception("DB 연결에 실패했습니다.");
            }
            
            Log.Info("DB 연결에 성공했습니다.");
        }
        
        public async Task<ClientData> Login(string name, string hashedPassword)
        {
            using var connection = NewConnection();
            await connection.OpenAsync();

            // 1. 계정 아이디에 해당하는 구문 찾기
            using var command = connection.NewCommand(
                "SELECT BIN_TO_UUID(id, 1), password FROM yachu.yachu_account WHERE (name = @name);"
            );
            command.Parameters.AddWithValue("@name", name);
            await command.PrepareAsync();
                
            using var reader = await command.ExecuteReaderAsync();
            // 실패 1: 아이디 없음 
            var readResult = await reader.ReadAsync();
            if (!readResult)
            {
                throw new InvalidAccountException($"{name} 로그인 실패: 아이디 유효하지 않음");
            }
            // 성공 1: 아이디 찾음
                
            // 2. 비밀번호 대조
            var guid = Guid.Parse(reader.GetString(0));
            var savedPassword = reader.GetString(1);
            // 실패 2: 비밀번호 해시 불일치
            if (!string.Equals(hashedPassword, savedPassword))
            {
                throw new InvalidPasswordException($"{name} 로그인 실패: 비밀번호 유효하지 않음");
            }
            // 성공 2: 비밀번호 일치
                
            return new ClientData { guid = guid, name = name, registered = true };
        }

        public async Task<ClientData> Register(string name, string hashedPassword)
        {
            using var connection = NewConnection();
            await connection.OpenAsync();

            // 쿼리 여러개 사용: 트랜잭션 사용
            using var transaction = await connection.BeginTransactionAsync();
            
            // 1. 계정 중복 테스트
            {
                using var query = connection.NewCommand(transaction, 
                    "SELECT COUNT(*) FROM yachu.yachu_account WHERE (name = @name);"
                );
                query.Parameters.AddWithValue("@name", name);
                await query.PrepareAsync();
            
                var resultCount = (long)await query.ExecuteScalarAsync();
                // 실패 1: 중복된 아이디 존재 
                if (resultCount >= 1)
                {
                    throw new DuplicatedNameException($"{name} 회원가입 실패: 이미 존재하는 아이디");
                }
                // 성공 1: 중복된 아이디 아님
            }
            
            Guid guid;
            // 2. 새로운 계정 INSERT
            {
                // 2.1. 먼저 yachu_account 테이블에 삽입
                {
                    using var query = connection.NewCommand(transaction,
                        "INSERT INTO yachu.yachu_account(name, password) VALUES(@name, @hashedPassword);"
                    );
                    query.Parameters.AddWithValue("@name", name);
                    query.Parameters.AddWithValue("@hashedPassword", hashedPassword);
                    await query.PrepareAsync();
                
                    var result = await query.ExecuteNonQueryAsync();
                    // 실패 2.1: 뭔가 단단히 잘못됨
                    if (result != 1)
                    {
                        await transaction.RollbackAsync();
                        throw new DuplicatedNameException($"{name} 회원가입 실패: 이미 존재하는 아이디");
                    }
                }
                // 성공 2.1: 성공적으로 insert
                
                // 2.2. 그 다음 yachu_user 테이블 삽입
                {
                    // 2.2.1. 생성된 UUID 가져오기
                    using var selectIdQuery = connection.NewCommand(transaction,
                        "SELECT BIN_TO_UUID(id, 1) FROM yachu.yachu_account WHERE name = @name;"
                    );
                    selectIdQuery.Parameters.AddWithValue("@name", name);
                    var selectIdResult = (string)await selectIdQuery.ExecuteScalarAsync();
                    if (selectIdResult == null)
                    {
                        await transaction.RollbackAsync();
                        throw new Exception($"{name} 회원가입 실패: DB SELECT account 실패");
                    }

                    try
                    {
                        guid = Guid.Parse(selectIdResult);
                    }
                    catch (Exception e)
                    {
                        await transaction.RollbackAsync();
                        throw new Exception($"{name} 회원가입 실패: UUID 변환 실패 - {selectIdResult}");
                    }
                    
                    // 2.2.2. 생성된 UUID 기반으로 user 테이블 생성
                    using var query = connection.NewCommand(transaction,
                        "INSERT INTO yachu.yachu_user(id) VALUES(UUID_TO_BIN(@id, 1));"
                    );
                    query.Parameters.AddWithValue("@id", guid.ToString());
                    await query.PrepareAsync();
                
                    var result = await query.ExecuteNonQueryAsync();
                    // 실패 2.2.2: 뭔가 단단히 잘못됨
                    if (result != 1)
                    {
                        // 트랜잭션 롤백
                        await transaction.RollbackAsync();
                        throw new DuplicatedNameException($"{name} 회원가입 실패: user DB 삽입 실패");
                    }
                }
                // 성공 2.2.2: 성공적으로 insert
            }
            await transaction.CommitAsync();
            
            return new ClientData { guid = guid, name = name, registered = true };
        }

        public async Task<bool> ChangeName(Client client, string newName)
    {
            var oldName = client.Name;
            using var connection = NewConnection();
            await connection.OpenAsync();

            using var query = connection.NewCommand(
                "UPDATE yachu.yachu_account SET name = @name WHERE id = UUID_TO_BIN(@id, 1)"
            );
            query.Parameters.AddWithValue("@name", newName);
            query.Parameters.AddWithValue("@id", client.Guid.ToString());
            await query.PrepareAsync();

            var result = await query.ExecuteNonQueryAsync();
            if (result != 1)
            {
                Log.Error($"{client} failed to ChangeName to {newName}");
                return false;
            }
            
            return true;
        }

        public async Task<UserData> GetUserData(Guid guid)
        {
            using var connection = NewConnection();
            await connection.OpenAsync();

            using var query = connection.NewCommand(
                "SELECT play_count, win_count, lose_count, money FROM yachu.yachu_user WHERE id = UUID_TO_BIN(@id, 1);"
            );
            query.Parameters.AddWithValue("@id", guid.ToString());
            await query.PrepareAsync();

            using var reader = await query.ExecuteReaderAsync();
            await reader.ReadAsync();
            var playCount = reader.GetInt32(0);
            var winCount = reader.GetInt32(1);
            var loseCount = reader.GetInt32(2);
            var money = reader.GetInt32(3);
            
            var userData = new UserData
            {
                Money = money,
                PlayCount = playCount,
                WinCount = winCount,
                LoseCount = loseCount
            };
            return userData;
            // try
            // {
            // }
            // catch (Exception e)
            // {
                // Console.WriteLine(e);
                // throw;
            // }
            
        }

        public async Task SetUserData(Guid guid, UserData userData)
        {
            using var connection = NewConnection();
            await connection.OpenAsync();

            using var query = connection.NewCommand(
                @"
INSERT INTO yachu.yachu_user(id, play_count, win_count, lose_count, money) 
VALUES(UUID_TO_BIN(@id, 1), @playCount, @winCount, @loseCount, @money) 
ON DUPLICATE KEY UPDATE play_count = @playCount, win_count = @winCount, lose_count = @loseCount, money = @money;
"
                // "UPDATE yachu.yachu_user SET win_count = @winCount, lose_count = @loseCount, money = @money WHERE id = @id;"
            );
            query.Parameters.AddWithValue("@playCount", userData.PlayCount);
            query.Parameters.AddWithValue("@winCount", userData.WinCount);
            query.Parameters.AddWithValue("@loseCount", userData.LoseCount);
            query.Parameters.AddWithValue("@money", userData.Money);
            query.Parameters.AddWithValue("@id", guid.ToString());
            await query.PrepareAsync();

            var result = await query.ExecuteNonQueryAsync();
            // if (result != 1)
            // {
                // throw new Exception($"SetUserData gone wrong: result - {result}");
            // }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Yachu.Server.Packets;

namespace Yachu.Server.Util {
    public static class ExtraUtil {
        
        // 현재 timestamp
        private static DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long CurrentTimeInMillis => 
            (long) ((DateTime.UtcNow - Jan1st1970).TotalMilliseconds);
        
        // 닉네임 랜덤 생성
        private static readonly char[] RandomAlphabets =
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        private static readonly int RandomAlphabetsSize = RandomAlphabets.Length;
        private static readonly Random Random = new();

        public static string GetRandomAlphabetsAndNumbers(int size = 5) {
            char[] chars = new char[size];
            for (int i = 0; i < size; i++) {
                chars[i] = RandomAlphabets[Random.Next(RandomAlphabetsSize)];
            }

            return new string(chars);
        }
        
        
        public static void SendPackets<T>(this IEnumerable<Client> clients, PacketData<T> packet) where T : PacketData<T> {
            SendPackets(clients, packet.ToPacket());
        }
        public static void SendPackets(this IEnumerable<Client> clients, Packet packet) {
            foreach (var client in clients) {
                client.SendPacket(packet);
            }
        }

        private static readonly SHA256 Hasher = SHA256.Create();
        /// <summary>
        /// 평문 비밀번호에 SHA256 해시함수를 적용합니다.
        /// </summary>
        /// <param name="rawPassword">평문 비밀번호입니다.</param>
        /// <returns>SHA256 해시입니다.</returns>
        public static string HashRawPassword(string rawPassword)
        {
            var encoder = Hasher;
            var hash = encoder.ComputeHash(Encoding.UTF8.GetBytes(rawPassword));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
        }
    }
}
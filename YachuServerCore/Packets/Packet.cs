using System;
using System.Runtime.InteropServices;

namespace Yachu.Server.Packets {


    public class Packet {
        public ushort Type { get; private set; }
        public byte[] Data { get; private set; }
        
        public int BodyLength { get; private set; } // 실제 body 배열의 길이
        public int Length { get; private set; }
        
        private const int TypeSize = sizeof(ushort);
        private const int DataLengthSize = sizeof(int);
        private const int HeadSize = TypeSize + DataLengthSize;

        public Packet(PacketType type) : this((ushort)type) { }

        public Packet(ushort type) {
            Type = type;
            SetData(Array.Empty<byte>(), 0);
        }
        public Packet(ushort type, byte[] data, int length) {
            Type = type;
            SetData(data, length);
        }

        // 생성 시에 헤더, 크기를 미리 가져다 둠
        public void SetData(byte[] data, int length) {
            Length = HeadSize + length;
            Data = new byte[Length];
            BodyLength = length;
            var typeInBytes = BitConverter.GetBytes(Type);
            var dataLengthInBytes = BitConverter.GetBytes(BodyLength);
            Array.Copy(typeInBytes, 0, Data, 0, TypeSize);
            Array.Copy(dataLengthInBytes, 0, Data, TypeSize, DataLengthSize);
            if(length > 0)
                Array.Copy(data, 0, Data, HeadSize, length);
        }
        
        // Deserialize 시 HeadSize만큼 source offset 주기
        public T GetPacketData<T>() where T : PacketData<T> {
            return PacketData<T>.Deserialize(Data, HeadSize);
        }
    }
    
    /// <summary>
    /// <para>
    /// 패킷 데이터 클래스입니다.
    /// 직렬화 가능한(Serializable) 구조체나 원시 타입만 있을 수 있습니다.
    /// </para>
    /// <para>
    /// 문자열의 경우는 <c>[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 문자열길이)]</c> 형태의 Attribute를 설정해야 합니다.
    /// 또한 문자열을 포함할 경우 클래스 Attribute 변수에 CharSet을 설정해야 합니다. 
    /// </para> 
    /// <para>
    /// 배열의 경우는 <c>[MarshalAs(UnmanagedType.ByValArray, SizeConst = 배열크기)]</c> 형태의 Attribute를 설정해야 합니다.
    /// </para> 
    /// <para>출처: <a href="https://shakddoo.tistory.com/entry/c-%EC%8B%A4%EC%8B%9C%EA%B0%84-%EC%86%8C%EC%BC%93-%EC%84%9C%EB%B2%84-%EB%A7%8C%EB%93%A4%EA%B8%B0-1-%ED%8C%A8%ED%82%B7">티스토리 블로그</a></para>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1)]// Pack=1 -> 1바이트 크기로 데이터를 맞춤
    public class PacketData<T> where T : PacketData<T> {
        protected static PacketType _packetType;
        // [NonSerialized] private PacketType _type;
        public PacketData(PacketType type) {
            _packetType = type;
            // _type = type;
        }

        // Packet structure to Byte array
        public byte[] Serialize() {
            var size = Marshal.SizeOf(typeof(T)); // 구조체 크기 구하기
            var array = new byte[size]; // 크기만큼의 바이트 배열 생성
            var ptr = Marshal.AllocHGlobal(size); // 크기만큼의 힙 메모리 생성
            Marshal.StructureToPtr(this, ptr, true); // 생성된 힙 메모리에 구조체 복사
            Marshal.Copy(ptr, array, 0, size); // 힙 메모리 바이트를 그대로 긁어서 바이트 배열에 복사 
            Marshal.FreeHGlobal(ptr); // 생성해둔 힙 메모리 해제
            return array; // 바이트 배열 반환
        }

        public Packet ToPacket() => ToPacket(_packetType);
        // public Packet ToPacket() => ToPacket(_type);
        public Packet ToPacket(PacketType type) {
            var data = Serialize();
            var packet = new Packet((ushort)type, data, data.Length);
            return packet;
        }

    
        // Byte array to Packet structure
        public static T Deserialize(byte[] array, int offset = 0) {
            var size = Marshal.SizeOf(typeof(T)); // 구조체 크기 구함
            var ptr = Marshal.AllocHGlobal(size); // 힙 메모리 생성
            Marshal.Copy(array, offset, ptr, size); // array의 offset구간부터 size만큼 ptr에 복사
            var structure = (T) Marshal.PtrToStructure(ptr, typeof(T)); // 힙 메모리를 그대로 구조체로 캐스팅
            Marshal.FreeHGlobal(ptr); // 힙 메모리 해제
            return structure; // 정상적으로 타입을 지정했다면, 구조체 멤버에는 데이터가 들어있을 것
        }
    }
}
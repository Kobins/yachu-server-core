using System;
using Yachu.Server.Util;

namespace Yachu.Server.Packets {
    /// <summary>
    /// byte 배열에서 Packet으로 변환하는 클래스
    /// </summary>
    public class PacketBuilder {
        public delegate void CompletedMessageCallback(Packet packet);

        private int _messageSize;
        private const int TypeBufferLength = sizeof(ushort);
        private const int HeaderBufferLength = sizeof(int);
        private const int MessageBufferLength = Constants.SocketBufferSize;
        private byte[] _typeBuffer = new byte[TypeBufferLength];
        private byte[] _headerBuffer = new byte[HeaderBufferLength];
        private byte[] _messageBuffer = new byte[MessageBufferLength];

        private PacketType _packetType;
        
        private int _typePosition;
        private int _headPosition;
        private int _currentPosition;
        
        private ushort _messageType;
        private int _remainBytes;
        
        private bool _typeCompleted;
        private bool _headCompleted;
        private bool _completed;

        private CompletedMessageCallback _completeCallback;

        public PacketBuilder() {
            ClearBuffer();
        }

        // TCP 특성 상 데이터가 온전하게 오지 않으므로, Read 시 제공되는 offset, transferred를 통해 여러번 호출으로 패킷을 만들 수 있도록 하는 메소드
        public void OnReceive(byte[] buffer, int offset, int transferred, CompletedMessageCallback callback) {
            // Log.Debug($"offset:{offset},transferred:{transferred}");
            // 현재 들어온 데이터의 위치를 저장
            int sourcePosition = offset;

            // 메시지 완성 시 콜백함수를 호출
            _completeCallback = callback;

            // 처리해야 할 메시지 양
            _remainBytes = transferred;

            // 한 바이트 배열에 여러 개 올 수 있으므로, 일단 반복은 함
            while (_remainBytes > 0) {
            
                // 패킷 종류 읽기
                if (!_typeCompleted) {
                
                    // Log.Debug($"- reading type, {sourcePosition}");
                    _typeCompleted = ReadType(buffer, ref sourcePosition);
                    if (!_typeCompleted) {
                        // Log.Debug($"- break while reading type");
                        return;
                    }

                    _messageType = BitConverter.ToUInt16(_typeBuffer, 0);

                    // 유효 검사
                    if (_messageType < 0 || _messageType > (int) PacketType.PacketCount - 1) {
                        // Log.Debug($"- invalid type({_messageType})");
                        return;
                    }

                    _packetType = (PacketType) _messageType;
                }
            
                // 헤더(데이터 사이즈) 읽기
                if (!_headCompleted) {
                    // Log.Debug($"- reading head, {sourcePosition}");
                    _headCompleted = ReadHead(buffer, ref sourcePosition);
                    if (!_headCompleted) {
                        // Log.Debug($"- break while reading head");
                        return;
                    }

                    _messageSize = BitConverter.ToInt32(_headerBuffer, 0);

                    if (_messageSize < 0 || _messageSize > 1024 * 2000) {
                        // Log.Debug($"- invalid body size({_messageSize})");
                        return;
                    }
                }
            
                // 바디 (데이터) 읽기
                if (!_completed && _messageSize > 0) {
                    // Log.Debug($"- reading body, {sourcePosition}");
                    _completed = ReadBody(buffer, ref sourcePosition);
                    if (!_completed) {
                        // Log.Debug($"- break while reading body");
                        return;
                    }
                }
            
                // 패킷 완성!!!
                var packet = new Packet(_messageType, _messageBuffer, _messageSize);
                _completeCallback(packet);

                // 다음 패킷 처리를 위해 비워두기
                _typeCompleted = false;
                _headCompleted = false;
                _completed = false;
                _typePosition = 0;
                _headPosition = 0;
                _currentPosition = 0;
            }
            // 완성 후 더 여지가 없으면 완전히 버퍼 초기화
            ClearBuffer();
        }

        private bool ReadType(byte[] buffer, ref int sourcePosition) {
            return ReadUntil(buffer, ref sourcePosition, _typeBuffer, ref _typePosition, 2);
        }
        private bool ReadHead(byte[] buffer, ref int sourcePosition) {
            return ReadUntil(buffer, ref sourcePosition, _headerBuffer, ref _headPosition, 4);
        }
        private bool ReadBody(byte[] buffer, ref int sourcePosition) {
            return ReadUntil(buffer, ref sourcePosition, _messageBuffer, ref _currentPosition, _messageSize);
        }
        
        private bool ReadUntil(byte[] buffer, ref int sourcePosition, byte[] destinationBuffer, ref int destinationPosition, int toSize) {
            // 남은 데이터가 없으면 아무것도 안 함
            if(_remainBytes <= 0) 
                return false;
            // 복사할 사이즈 = 읽으려는 바이트 - 읽은 위치
            int copySize = toSize - destinationPosition; 
            // 부족하면 fitting
            if (_remainBytes < copySize) 
                copySize = _remainBytes;
            // 배열 복사
            Array.Copy(
                buffer, sourcePosition, 
                destinationBuffer, destinationPosition, 
                copySize
            );
            
            // 읽은 위치 정보 전달
            sourcePosition      += copySize; 
            destinationPosition += copySize; 
            _remainBytes       -= copySize;
            
            // 원하는 만큼 읽었으면 true, 아니면 아직 덜 읽었으므로 false
            return destinationPosition >= toSize; 
        }

        public void ClearBuffer() {
            Array.Clear(_messageBuffer, 0, MessageBufferLength);
            Array.Clear(_headerBuffer, 0, HeaderBufferLength);
            Array.Clear(_typeBuffer, 0, TypeBufferLength);

            _messageSize = 0;
            _headPosition = 0;
            _typePosition = 0;
            _currentPosition = 0;
            _messageType = 0;
            // _remainBytes = 0;
            _headCompleted = false;
            _typeCompleted = false;
            _completed = false;
        }
    }
}
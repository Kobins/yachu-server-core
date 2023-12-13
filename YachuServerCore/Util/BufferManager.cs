using System.Collections.Generic;
using System.Net.Sockets;

namespace Yachu.Server.Util {
    // https://shakddoo.tistory.com/entry/c-SocketAsyncEventArgs-%EB%A9%94%EB%AA%A8%EB%A6%AC-%EB%A6%AD-%ED%98%84%EC%83%81
    // 버퍼 풀링
    public class BufferManager : Singleton<BufferManager> {
        private int _numBytes; // 버퍼 풀에 의해 관리되는 바이트 수
        private byte[] _buffer; // 미리 생성된 버퍼
        private Stack<int> _freeIndexPool;
        private int _currentIndex;
        private int _bufferSize;

        protected override void Initialize() {
            _numBytes = Constants.MaxConnection * Constants.SocketBufferSize * 2;
            _currentIndex = 0;
            _bufferSize = Constants.SocketBufferSize;
            _freeIndexPool = new Stack<int>();
            _buffer = new byte[_numBytes];
        }

        public bool SetBuffer(SocketAsyncEventArgs e) {
            // 누군가 반환한 여유 버퍼가 있을 경우
            if (_freeIndexPool.Count > 0) {
                e.SetBuffer(_buffer, _freeIndexPool.Pop(), _bufferSize);
                return true;
            }
            // 남은 버퍼가 부족할 경우 -> false
            else if (_numBytes < (_currentIndex + _bufferSize)) {
                return false;
            }
            // 순서대로 쌓기
            e.SetBuffer(_buffer, _currentIndex, _bufferSize);
            _currentIndex += _bufferSize;
            return true;
        }

        public void FreeBuffer(SocketAsyncEventArgs e) {
            if (e == null) return;
            _freeIndexPool.Push(e.Offset);
            e.SetBuffer(null, 0, 0);
            // 가끔 SocketAsyncEventArgs에서 사용중이라고 예외 발생가능하기 때문에?, 이 함수 밖에서 처리함?
        }
    }
}
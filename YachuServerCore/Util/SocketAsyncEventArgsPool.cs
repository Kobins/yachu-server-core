using System.Collections.Generic;
using System.Net.Sockets;

namespace Yachu.Server.Util {
    // https://shakddoo.tistory.com/entry/c-SocketAsyncEventArgs-%EB%A9%94%EB%AA%A8%EB%A6%AC-%EB%A6%AD-%ED%98%84%EC%83%81
    // 오브젝트 풀링
    public class SocketAsyncEventArgsPool : Singleton<SocketAsyncEventArgsPool> {
        private Stack<SocketAsyncEventArgs> _pool;

        protected override void Initialize() {
            _pool = new Stack<SocketAsyncEventArgs>(Constants.MaxConnection * 2);
            for (int i = 0; i < Constants.MaxConnection * 2; i++) {
                var eventArgs = new SocketAsyncEventArgs();
                _pool.Push(eventArgs);
            }
        }

        public void Push(SocketAsyncEventArgs item) {
            if (item == null) {
                return;
            }

            lock (_pool) {
                // 할당 갯수를 넘으면 그냥 버림
                if (_pool.Count >= Constants.MaxConnection) {
                    item.Dispose();
                    return;
                }
                _pool.Push(item);
            }
        }

        public SocketAsyncEventArgs Pop() {
            lock (_pool) {
                if (_pool.Count > 0) {
                    return _pool.Pop();
                }
                // 부족하면 일단 새로 꺼냄
                var eventArgs = new SocketAsyncEventArgs();
                return eventArgs;
            }
        }
        
        public int Count => _pool.Count;
    }
}
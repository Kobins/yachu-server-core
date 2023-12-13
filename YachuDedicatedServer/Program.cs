using System;
using System.Diagnostics;
using System.Threading;
using Yachu.Server;
using Yachu.Server.Util;

namespace Yachu.DedicatedServer {
    internal class Program {
        public static void Main(string[] args) {
            Log.Printer = ConsoleLogPrinter.Instance;
            new DedicatedServer().Start();
        }
    }

    class DedicatedServer : IDisposable {
        private YachuGameServer _server;
        private Thread _tickThread;
        public DedicatedServer() {
            _server = new YachuGameServer();
        }

        public void Start() {
            if (_server == null) {
                return;
            }
            _server.Start();
            
            _tickThread = new Thread(TickProcess) { Name = "Dedicated Tick Thread" };
            _tickThread.Start();
        }

        public void Dispose() {
            if (_server != null) {
                _tickThread.Interrupt();
            }
        }

        private void TickProcess() {
            try {
                var stopwatch = new Stopwatch();
                while (true) {
                    stopwatch.Restart();
                    _server.Tick();
                    stopwatch.Stop();
                    var elapsed = stopwatch.ElapsedMilliseconds;
                    var sleepTime = Constants.TickInMilliseconds - elapsed;
                    if (sleepTime > 0) {
                        Thread.Sleep((int)sleepTime);
                    }
                }
            }
            catch (ThreadInterruptedException e) {
                
            }
            // 종료
            _server.Dispose();
            _server = null;
        }
    }
}
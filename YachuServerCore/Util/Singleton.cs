namespace Yachu.Server.Util {
    public class Singleton<T> where T : Singleton<T>, new() {
        protected virtual void Initialize() {}

        private static object _createLock = new();
        protected static T _instance;
        public static T Instance {
            get {
                if (_instance == null) {
                    lock(_createLock) {
                        if (_instance == null) {
                            _instance = new T();
                            _instance.Initialize();
                        }
                    }
                }

                return _instance;
            }
        }
    }
}
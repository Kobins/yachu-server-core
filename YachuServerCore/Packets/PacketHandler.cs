using System.Collections.Generic;
using Yachu.Server.Util;

namespace Yachu.Server.Packets {
    public class Unit {
        private Unit() {}
        public static readonly Unit Instance = new();
    }
    
    public delegate void PacketProcessor<in T>(Packet packet, T args);

    public class PacketListener<T> {
        public readonly PacketType type;
        public readonly PacketProcessor<T> processor;

        public PacketListener(PacketType type, PacketProcessor<T> processor) {
            this.type = type;
            this.processor = processor;
        }
    }

    public class PacketHandler<T> : Singleton<PacketHandler<T>> where T : class {
        private readonly PacketProcessor<T>[] _packetListeners = new PacketProcessor<T>[(int)PacketType.PacketCount];

        public void RegisterListener(PacketType type, PacketProcessor<T> processor) {
            // Log.Info($"PacketListener registered at {type.ToString()}");
            _packetListeners[(int)type] += processor;
        }

        public static void RegisterListener(PacketListener<T> listener) => Instance.RegisterListener(listener.type, listener.processor);
        public static List<TListener> RegisterListeners<TListener>(List<TListener> listeners) where TListener : PacketListener<T>{
            var instance = Instance;
            foreach (var listener in listeners) {;
                instance.RegisterListener(listener.type, listener.processor);
            }

            return listeners;
        }
    
        public static void UnregisterListener(PacketListener<T> listener) => Instance.RegisterListener(listener.type, listener.processor);
        public static void UnregisterListeners<TListener>(List<TListener> listeners) where TListener : PacketListener<T> {
            var instance = Instance;
            foreach (var listener in listeners) {
                instance.UnregisterListener(listener.type, listener.processor);
            }
        }
        public void UnregisterListener(PacketType type, PacketProcessor<T> processor) {
            _packetListeners[(int)type] -= processor;
        }
    
    
        public void HandlePacket(Packet packet, T args = null) {
            var processor = _packetListeners[packet.Type];
            processor?.Invoke(packet, args);
        }
    
    }

    
}
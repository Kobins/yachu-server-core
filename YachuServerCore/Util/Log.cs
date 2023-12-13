using System;
using System.IO;
using System.Threading;

namespace Yachu.Server.Util {
    public static class Log {
        public enum LogType {
            NORMAL,
            ERROR,
        }

        public static ILogPrinter Printer { get; set; } = ConsoleLogPrinter.Instance;
        
        public static bool PrintThreadInformation { get; set; } = false;
        public static bool LogDebug { get; set; } = true;
        public static string Now => DateTime.Now.ToString("HH:mm:ss");

        private static int AnonymousThreadNumber { get; set; } = 0;

        public static string ThreadPrefix 
            => Thread.CurrentThread.Name ??= $"Anonymous Thread #{AnonymousThreadNumber++}";

        public static void Info<T>(T message) {
            Printer.Print(LogType.NORMAL, "INFO", message);
        }
        public static void Debug<T>(T message) {
            if(!LogDebug) return;
            Printer.Print(LogType.NORMAL, "DEBUG", message);
        }

        public static void Warn<T>(T message) {
            Printer.Print(LogType.NORMAL, "WARN", message);
        }

        public static void Error<T>(T message) {
            Printer.Print(LogType.ERROR, "ERROR", message);
        }
    }

    public interface ILogPrinter {
        public void Print<T>(Log.LogType type, string prefix, T message);
    }

    public class ConsoleLogPrinter : Singleton<ConsoleLogPrinter>, ILogPrinter {
        public void Print<T>(Log.LogType type, string prefix, T message) {
            var writer = type == Log.LogType.NORMAL ? Console.Out : Console.Error;
            Print(writer, prefix, message);
        }
        private void Print<T>(TextWriter writer, string prefix, T message) {
            writer.Write($"[{prefix}] ");
            if (Log.PrintThreadInformation) {
                writer.Write($"[{Log.ThreadPrefix}] ");
            }
            writer.Write($"[{Log.Now}] ");
            writer.WriteLine(message);
        }
    }
}
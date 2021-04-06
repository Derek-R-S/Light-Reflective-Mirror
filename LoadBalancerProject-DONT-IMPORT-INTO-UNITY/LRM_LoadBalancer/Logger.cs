using System;

namespace LightReflectiveMirror.Debug
{
    public static class Logger
    {
        private static LogConfiguration conf;

        public static void ConfigureLogger(LogConfiguration config) => conf = config;

        public static void WriteLogMessage(string message, ConsoleColor color = ConsoleColor.White, bool oneLine = false)
        {
            if(!conf.sendLogs) { return; }

            Console.ForegroundColor = color;
            if (oneLine)
                Console.Write(message);
            else
                Console.WriteLine(message);
        }

        public static void ForceLogMessage(string message, ConsoleColor color = ConsoleColor.White, bool oneLine = false)
        {
            Console.ForegroundColor = color;

            if (oneLine)
                Console.Write(message);
            else
                Console.WriteLine(message);
        }

        public struct LogConfiguration
        {
            public bool sendLogs;
        }
    }
}

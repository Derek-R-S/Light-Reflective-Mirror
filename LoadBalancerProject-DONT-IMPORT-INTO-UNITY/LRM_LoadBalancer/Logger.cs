using System;

namespace LightReflectiveMirror.Debug
{
    public static class Logger
    {
        private static LogConfiguration _conf;

        public static void ConfigureLogger(LogConfiguration config) => _conf = config;

        public static void WriteLogMessage(string message, ConsoleColor color = ConsoleColor.White, bool oneLine = false)
        {
            if(!_conf.sendLogs) { return; }

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

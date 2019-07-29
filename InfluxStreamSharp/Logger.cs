using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace InfluxStreamSharp
{
    public class Logger
    {
        private static object _logLockObj = new object();

        /// <summary>
        /// 是否在控制台输出日志的开关
        /// </summary>
        public static bool IsEchoOn = true;

        public string Prefix { get; set; } = string.Empty;

        public Logger()
        {
            Console.OutputEncoding = Encoding.UTF8;
        }

        public Logger(string prefix):this()
        {
            Prefix = prefix;
        }

        public void LogInfo(string text)
        {
            SimpleLog(text);
        }

        public void LogError(string text)
        {
            SimpleLog(text);
        }

        private void SimpleLog(string text)
        {
            if (IsEchoOn)
            {
                lock (_logLockObj)
                {
                    Console.Write(DateTime.Now.ToString("HH:mm:ss"));
                    Console.Write(" ");
                    if (!string.IsNullOrEmpty(Prefix))
                    {
                        Console.Write("[");
                        Console.Write(Prefix);
                        Console.Write("] ");
                    }
                    Console.WriteLine(text);
                }
            }
        }
    }
}

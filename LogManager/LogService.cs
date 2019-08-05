using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogManager
{
    class LogService : IDisposable
    {
        /// <summary>
        /// 单例模式
        /// </summary>
        public static readonly Lazy<LogService> Instance = new Lazy<LogService>(() => new LogService());

        private string LogPath;

        private BlockingCollection<(LogTypeEnum, string, bool)> _logQueue;

        private bool _writeThreadAlive = false;
        private bool _canWriteThreadRun = false;
        private Thread _logWriterThread = null;

        private const int CONST_LOG_EXPIRED_DAYS = 5;//日志过期时间为5天
        private DateTime _lastCleanLogDate = DateTime.Now.AddDays(-10);

        private LogService()
        {
            //创建日志输出目录
            LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(LogPath))
            {
                Directory.CreateDirectory(LogPath);
            }
            //初始化写入队列
            _logQueue = new BlockingCollection<(LogTypeEnum, string, bool)>();

            //启动日志输出线程
            _canWriteThreadRun = true;
            _logWriterThread = new Thread(new ThreadStart(LogWriterWorkerThread));
            _logWriterThread.Name = "LogWriterWorkerThread";
            _logWriterThread.IsBackground = true;
            _logWriterThread.Start();
        }

        public void Dispose()
        {
            _canWriteThreadRun = false;
        }

        public void LogDebug(string logStr, bool writeToFile = true)
        {
            if (_writeThreadAlive)
                _logQueue.Add((LogTypeEnum.LogDebug, logStr, writeToFile));
        }

        public void LogError(string logStr, bool writeToFile = true)
        {
            if (_writeThreadAlive)
                _logQueue.Add((LogTypeEnum.LogError, logStr, writeToFile));
        }

        private string GetLogFilePath(LogTypeEnum logType)
        {
            string fileName = $"{logType.ToString()}_{DateTime.Now.ToString("yyyy-MM-dd")}.txt";
            return Path.Combine(LogPath, fileName);
        }

        private void LogWriterWorkerThread()
        {
            _writeThreadAlive = true;
            try
            {
                while (_canWriteThreadRun)
                {
                    //如果需要，则清理日志
                    CleanIfNeeded();
                    //尝试取出待打印的日志
                    if (_logQueue.TryTake(out var log, 5000))
                    {
                        var (logtype, logStr, writeToFile) = log;

                        if (logtype == LogTypeEnum.LogError) Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(logStr);
                        if (logtype == LogTypeEnum.LogError) Console.ForegroundColor = ConsoleColor.Gray;

                        if (writeToFile)
                        {
                            //获取输出文件名
                            string filename = GetLogFilePath(logtype);
                            //写入文件
                            File.AppendAllText(filename, logStr + "\r\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    string errmsg = "[LogService]输出日志时出错，原因：" + ex.Message;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(errmsg);
                    Console.ForegroundColor = ConsoleColor.Gray;

                    File.AppendAllText(GetLogFilePath(LogTypeEnum.LogError), errmsg + "\r\n");
                }
                catch (Exception) { }
            }
            _writeThreadAlive = false;
        }

        private void CleanIfNeeded()
        {
            //清理若干天前的日志
            if (_lastCleanLogDate.AddDays(CONST_LOG_EXPIRED_DAYS) < DateTime.Now)
            {
                DeleteOutdateFiles(LogPath, DateTime.Now.AddDays(CONST_LOG_EXPIRED_DAYS * -1));

                _lastCleanLogDate = DateTime.Now;
            }
        }

        /// <summary>
        /// 删除指定目录下的过期文件
        /// </summary>
        /// <param name="directory">指定目录</param>
        /// <param name="daysbefore">过期时间</param>
        private static void DeleteOutdateFiles(string directory, DateTime daysbefore)
        {
            if (Directory.Exists(directory))
            {
                DirectoryInfo d = new DirectoryInfo(directory);
                string basePath = d.FullName;
                foreach (var file in d.GetFiles())
                {
                    //比较文件创建时间
                    if (file.CreationTime < daysbefore)
                    {
                        //删除过期文件
                        try
                        {
                            file.Delete();
                        }
                        catch (Exception) { }
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogManager
{
    public class LogService : IDisposable
    {
        /// <summary>
        /// 单例模式
        /// </summary>
        public static readonly Lazy<LogService> Instance = new Lazy<LogService>(() => new LogService());

        private static string LogPath;

        private BlockingCollection<(LogTypeEnum, string, bool)> _logQueue;

        private bool _writeThreadAlive = false;
        private bool _canWriteThreadRun = false;
        private Thread _logWriterThread = null;
        private CancellationTokenSource _queueCancelSource = new CancellationTokenSource();

        private const int CONST_LOG_EXPIRED_DAYS = 5;//日志过期时间为5天
        private DateTime _lastCleanLogDate = DateTime.Now.AddDays(-10);

        private static string _logFolder = @"tlogs";

        /// <summary>
        /// 读取或者修改日志输出文件夹
        /// </summary>
        public static string LogFolder
        {
            get => _logFolder;
            set
            {
                if (_logFolder?.Equals(value) == false)
                {
                    _logFolder = value;
                    //创建日志输出目录
                    LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _logFolder);
                    CreateLogFolderIfNotExist();
                }
            }
        }

        /// <summary>
        /// 如果日志输出文件夹不存在则创建
        /// </summary>
        private static void CreateLogFolderIfNotExist()
        {
            if (!Directory.Exists(LogPath))
            {
                Directory.CreateDirectory(LogPath);
            }
        }

        private LogService()
        {
            //创建日志输出目录
            LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFolder);
            CreateLogFolderIfNotExist();

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
            _queueCancelSource.Cancel();
            _canWriteThreadRun = false;
            _logWriterThread?.Join();
        }

        /// <summary>
        /// 输出调试信息
        /// </summary>
        /// <param name="logStr"></param>
        /// <param name="writeToFile"></param>
        public void LogDebug(string logStr, bool writeToFile = true)
        {
            if (_logWriterThread?.IsAlive == true)
                _logQueue.Add((LogTypeEnum.LogDebug, GetDetail(logStr), writeToFile));
        }

        /// <summary>
        /// 输出错误信息
        /// </summary>
        /// <param name="logStr"></param>
        /// <param name="writeToFile"></param>
        public void LogError(string logStr, bool writeToFile = true)
        {
            if (_logWriterThread?.IsAlive == true)
                _logQueue.Add((LogTypeEnum.LogError, GetDetail(logStr), writeToFile));
        }

        public void LogTrace(string logStr, bool writeToFile = true)
        {
            if (_logWriterThread?.IsAlive == true)
                _logQueue.Add((LogTypeEnum.LogTrace, GetDetail(logStr), writeToFile));
        }

        /// <summary>
        /// 获取调用堆栈详情
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private string GetDetail(string message)
        {
            var stackTrace = new StackTrace(true);
            var indexOfStack = 2;
            try
            {
                var stackFrame = stackTrace.GetFrame(indexOfStack);
                if (stackFrame != null)
                {
                    return $"{DateTime.Now.ToString("HH:mm:ss")} [{stackFrame.GetMethod().DeclaringType.Name}][{stackFrame.GetMethod().Name}] {message}";
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(string.Format("{0} ,exception: {1}", message, ex.Message));
            }
            return message;
        }

        /// <summary>
        /// 获取到日志输出的文件名
        /// </summary>
        /// <param name="logType"></param>
        /// <returns></returns>
        private string GetLogFilePath(LogTypeEnum logType)
        {
            string fileName = $"{logType.ToString()}_{DateTime.Now.ToString("yyyy-MM-dd")}.txt";
            return Path.Combine(LogPath, fileName);
        }

        /// <summary>
        /// 打印日志的线程
        /// </summary>
        private void LogWriterWorkerThread()
        {
            _writeThreadAlive = true;
            try
            {
                while (_canWriteThreadRun || _logQueue.Count > 0)
                {
                    //如果需要，则清理日志
                    CleanIfNeeded();
                    //尝试取出待打印的日志
                    if (_logQueue.TryTake(out var log, 5000, _queueCancelSource.Token))
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
                            TryAppendText(filename, logStr + "\r\n");
                            //File.AppendAllText(filename, logStr + "\r\n");
                            //AppendLine(filename, logStr);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //已取消读取日志队列
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

        private void TryAppendText(string filePath, string errmsg)
        {
            try
            {
                File.AppendAllText(filePath, errmsg);
            }
            catch (Exception) { }
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

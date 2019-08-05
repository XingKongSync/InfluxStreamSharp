using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogManager
{
    public class Logger
    {
        public string Prefix { get; set; } = string.Empty;

        private LogService _logService;

        public Logger()
        {
            _logService = LogService.Instance.Value;
        }

        public Logger(string prefix):this()
        {
            Prefix = prefix;
        }

        public void LogDebug(string text, bool writeToFile = true)
        {
            LogData(LogTypeEnum.LogDebug, text, writeToFile);
        }

        public void LogError(string text, bool writeToFile = true)
        {
            LogData(LogTypeEnum.LogError, text, writeToFile);
        }

        public void LogData(LogTypeEnum logType, string text, bool writeToFile)
        {
            string logStr = $"{DateTime.Now.ToString("HH:mm:ss")} ";

            if (!string.IsNullOrEmpty(Prefix))
            {
                logStr += $"[{Prefix}]";
            }
            logStr += text;

            switch (logType)
            {
                case LogTypeEnum.LogDebug:
                    _logService.LogDebug(logStr, writeToFile);
                    break;
                case LogTypeEnum.LogError:
                    _logService.LogError(logStr, writeToFile);
                    break;
                default:
                    break;
            }
        }
    }
}

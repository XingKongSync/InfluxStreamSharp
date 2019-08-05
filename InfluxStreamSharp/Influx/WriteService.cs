using AdysTech.InfluxDB.Client.Net;
using LogManager;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxStreamSharp.Influx
{
    /// <summary>
    /// 维护写入队列
    /// 合并写入请求
    /// 通过InfluxService写入数据
    /// </summary>
    public class WriteService
    {
        static Logger _logger = new Logger("WriteService");
        public static readonly Lazy<WriteService> Instance = new Lazy<WriteService>(() => new WriteService());

        private ConcurrentQueue<InfluxDatapoint<InfluxValueField>> BufferedQueue = new ConcurrentQueue<InfluxDatapoint<InfluxValueField>>();

        private object _startStopLockObj = new object();
        private const int TIMER_DELAY = 1000;
        private Thread TimerThread;
        private bool _timerIsEnabled = false;
        private bool _isTimerRunning = false;

        private WriteService() { }

        /// <summary>
        /// 启动写入队列
        /// </summary>
        public void Start()
        {
            lock (_startStopLockObj)
            {
                if (!_timerIsEnabled)
                {
                    _timerIsEnabled = true;

                    ThreadStart tInfo = new ThreadStart(TimerThreadWorker);
                    TimerThread = new Thread(tInfo);
                    TimerThread.Start();
                }
            }
        }

        /// <summary>
        /// 停止写入队列
        /// </summary>
        public void Stop()
        {
            lock (_startStopLockObj)
            {
                if (_timerIsEnabled)
                {
                    _isTimerRunning = false;
                    int waitCount = 0;
                    bool success = true;
                    while (_isTimerRunning)
                    {
                        Task.Delay(TIMER_DELAY).Wait();
                        waitCount++;
                        if (waitCount >= 10)
                        {
                            success = false;
                            break;
                        }
                    }

                    if (!success)
                    {
                        _logger.LogError("等等写入队列停止超时");
                    }
                }
            }
        }

        /// <summary>
        /// 工作线程
        /// </summary>
        private void TimerThreadWorker()
        {
            _isTimerRunning = true;

            _logger.LogDebug("Influx写入队列启动");
            List<InfluxDatapoint<InfluxValueField>> tempWriteList = new List<InfluxDatapoint<InfluxValueField>>();
            while (_timerIsEnabled)
            {
                tempWriteList.Clear();
                while (BufferedQueue.TryDequeue(out InfluxDatapoint<InfluxValueField> data))
                {
                    tempWriteList.Add(data);
                }

                DateTime startWriteTime = DateTime.Now;
                if (tempWriteList.Count > 0)
                {
                    InfluxService.Instance.Value.WriteAsync(tempWriteList).Wait();
                    _logger.LogDebug($"写入InfluxDB记录：{tempWriteList.Count} 条");
                }
                DateTime endWriteTime = DateTime.Now;

                int delayTime = (int)(TIMER_DELAY - (endWriteTime - startWriteTime).TotalMilliseconds);

                delayTime = delayTime > 0 ? delayTime : TIMER_DELAY;
                Task.Delay(delayTime).Wait();
            }

            _logger.LogDebug("Influx写入队列终止");
            _isTimerRunning = false;
        }

        /// <summary>
        /// 数据入队
        /// </summary>
        /// <param name="data"></param>
        public void Enqueue(InfluxDatapoint<InfluxValueField> data)
        {
            BufferedQueue.Enqueue(data);
        }
    }
}

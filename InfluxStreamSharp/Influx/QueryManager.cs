using LogManager;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxStreamSharp.Influx
{
    public class QueryManager
    {
        public enum PlayStatusEnum
        {
            /// <summary>
            /// 已停止播放
            /// </summary>
            Stopped,

            /// <summary>
            /// 正在播放
            /// </summary>
            Playing,

            /// <summary>
            /// 等待缓冲结束
            /// </summary>
            WairForBuffing
        }

        private static LogService _logger = LogService.Instance.Value;

        public delegate void DataReceivedDelegate(object data);

        private DateTime TimeBegin;
        private DateTime TimeEnd;

        private List<IQueryWorker> QueryWorkers;
        private Thread TimerThread;
        private bool _timerIsEnabled = false;
        private bool _isTimerRunning = false;

        private object _eventLockObj = new object();
        private DataReceivedDelegate _dataReceived;
        
  
        private DateTime _currentPlayTime;
        private PlayStatusEnum _currentPlayStatus = PlayStatusEnum.Stopped;

        /// <summary>
        /// 当前播放状态
        /// </summary>
        public PlayStatusEnum CurrentPlayStatus
        {
            get => _currentPlayStatus;
            private set
            {
                if (_currentPlayStatus != value)
                {
                    _currentPlayStatus = value;
                    _logger.LogDebug($"CurrentPlayStatus: {CurrentPlayStatus.ToString()}");
                }
            }
        }

        /// <summary>
        /// 当前播放到的时间
        /// </summary>
        public DateTime CurrentPlayTime { get => _currentPlayTime; private set => _currentPlayTime = value; }

        /// <summary>
        /// 计时器触发，推送数据
        /// </summary>
        public event DataReceivedDelegate DataReceived
        {
            add { lock (_eventLockObj) _dataReceived += value; }
            remove { lock (_eventLockObj) _dataReceived -= value; }
        }

        public QueryManager(DateTime _timeBegin, DateTime _timeEnd)
        {
            TimeBegin = _timeBegin;
            TimeEnd = _timeEnd;
        }

        /// <summary>
        /// 添加查询条件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tmp"></param>
        public void AddInfluxQueryTemplet<T>(InfluxQLTemplet tmp)
        {
            if (QueryWorkers == null)
                QueryWorkers = new List<IQueryWorker>();

            QueryWorker<T> worker = new QueryWorker<T>(TimeBegin, TimeEnd, tmp);
            QueryWorkers.Add(worker);
        }

        /// <summary>
        /// 触发推送数据事件
        /// 建议不要在回调函数中调用Start或者Stop方法
        /// </summary>
        /// <param name="data"></param>
        private void InvokeDataReceivedEvent(object data)
        {
            lock (_eventLockObj)
            {
                _dataReceived?.Invoke(data);
            }
        }

        public async Task Start()
        {
            if (!_timerIsEnabled)
            {
                _timerIsEnabled = true;

                //重置开始时间
                CurrentPlayTime = TimeBegin;
                foreach (IQueryWorker worker in QueryWorkers)
                {
                    await worker.Init();
                }

                ThreadStart tInfo = new ThreadStart(TimerThreadWorker);
                TimerThread = new Thread(tInfo);
                TimerThread.Start();
            }
        }

        public async Task Stop()
        {
            if (_timerIsEnabled)
            {
                _timerIsEnabled = false;
                int waitCount = 0;
                bool success = true;
                //等待计时器线程退出
                while (_isTimerRunning)
                {
                    await Task.Delay(200);
                    waitCount++;
                    if (waitCount >= 10)
                    {
                        success = false;
                        break;
                    }
                }

                if (!success)
                {
                    _logger.LogError("等待计时器线程退出超时，请检查回调函数中是否有函数卡死，或者有多个线程调用了Start或者Stop方法");
                }
            }
        }

        /// <summary>
        /// 计时器最低的延时时间
        /// 每次循环都会是当前播放时间向前推进
        /// 如果加载速度跟不上播放速度，则会等待加载完成再播放
        /// </summary>
        private const int TIMER_DELAY = 100;

        private void TimerThreadWorker()
        {
            _isTimerRunning = true;
            CurrentPlayStatus = PlayStatusEnum.Playing;
            while (_timerIsEnabled)
            {
                //判断是否达到播放结尾
                if (CurrentPlayTime >= TimeEnd)
                {
                    CurrentPlayStatus = PlayStatusEnum.Stopped;
                    break;
                }

                //记录循环开始的时间，用于精确计算推进时间
                DateTime loopBeginTime = DateTime.Now;
                double timeShift = TIMER_DELAY;

                //没有达到播放结尾
                foreach (IQueryWorker worker in QueryWorkers)
                {
                    var dataCollection = worker.Dequeue(CurrentPlayTime);
                    if (dataCollection != null)
                    {
                        foreach (object data in dataCollection)
                        {
                            //向外部推送数据
                            InvokeDataReceivedEvent(data);
                        }
                    }
                    //判断是否需要预载数据
                    if (worker.IsLoadingData && worker.IfNeedLoad(CurrentPlayTime))
                    {
                        //如果worker正忙于加载数据，而此时再次需要加载数据
                        //则认为播放卡住了，应该等待加载完成
                        CurrentPlayStatus = PlayStatusEnum.WairForBuffing;
                        worker.LoadDataIfNeeded(CurrentPlayTime).Wait();
                        CurrentPlayStatus = PlayStatusEnum.Playing;
                    }
                    else
                    {
                        worker.LoadDataIfNeeded(CurrentPlayTime);
                        //正常播放，时间推进应该包括在循环内消耗的时间
                        timeShift = (DateTime.Now - loopBeginTime).TotalMilliseconds + TIMER_DELAY;
                    }
                    //播放时间推进
                    CurrentPlayTime = CurrentPlayTime.AddMilliseconds(timeShift);
                }
                Task.Delay(TIMER_DELAY).Wait();
            }
            _isTimerRunning = false;
        }
    }
}

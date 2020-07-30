using LogManager;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxStreamSharp.Influx
{
    public class ChunkedQueryWorker<T>
    {
        private static LogService _logger = LogService.Instance.Value;

        public enum WorkerStatusEnum
        {
            Idle,
            Loading,
            Completed,
            Canceled
        }

        private DateTime TimeBegin;
        private DateTime TimeEnd;
        private InfluxQLTemplet InfluxQLTemplet;
        private TimeSpliter Spliter;
        private InfluxService InfluxDB;

        private BlockingCollection<InfluxQueryItem<T>> _databuffer;
        private WorkerStatusEnum _workerStatus = WorkerStatusEnum.Idle;
        private Thread _loadDataThread;

        public event Action<int, int> ProgressChanged;

        public BlockingCollection<InfluxQueryItem<T>> Databuffer
        {
            get
            {
                return _databuffer;
            }
        }

        public WorkerStatusEnum WorkerStatus { get => _workerStatus; private set => _workerStatus = value; }

        /// <summary>
        /// 数据加载的进度
        /// </summary>
        public int DataLoadProgressPercent
        {
            get
            {
                if (Spliter != null)
                {
                    return Math.Min(((Spliter.CurrentChunkIndex + 1) * 100 / Spliter.ChunkCount), 100);
                }
                return 0;
            }
        }

        public ChunkedQueryWorker(DateTime beginTime, DateTime endTime, InfluxQLTemplet influxQLTemplet)
        {
            TimeBegin = beginTime;
            TimeEnd = endTime;
            InfluxQLTemplet = influxQLTemplet;
            Spliter = new TimeSpliter(beginTime, endTime);
            InfluxDB = InfluxService.Instance.Value;
        }

        public bool Start()
        {
            lock (this)
            {
                if (WorkerStatus != WorkerStatusEnum.Loading)
                {
                    Spliter.CurrentChunkIndex = 0;
                    _databuffer = new BlockingCollection<InfluxQueryItem<T>>();
                    _loadDataThread = new Thread(new ThreadStart(DataLoadThreadWorker));
                    _loadDataThread.IsBackground = false;
                    _loadDataThread.Start();
                    return true;
                }
            }
            return false;
        }

        public void Stop()
        {
            lock (this)
            {
                if (WorkerStatus == WorkerStatusEnum.Loading)
                {
                    WorkerStatus = WorkerStatusEnum.Canceled;
                }
            }
            //等待线程终止
            if (_loadDataThread?.IsAlive == true)
                _loadDataThread?.Join();
            _loadDataThread = null;
        }

        private void DataLoadThreadWorker()
        {
            WorkerStatus = WorkerStatusEnum.Loading;

            while (WorkerStatus == WorkerStatusEnum.Loading && Spliter.NextChunk(out DateTime buffTimeBegin, out DateTime buffTimeEnd))
            {
                InfluxQLTemplet.LocalBeginTime = buffTimeBegin;
                InfluxQLTemplet.LocalEndTime = buffTimeEnd;
                string influxQL = InfluxQLTemplet.GetInfluxQL();

                _logger.LogDebug("开始加载Influx数据，InfluxQL：" + influxQL);

                //加载当前块的数据
                List<InfluxQueryItem<T>> result = InfluxDB.Query<T>(influxQL).Result;
                if (result != null)
                {
                    foreach (var data in result)
                    {
                        Databuffer.Add(data);
                    }
                }
                //通知调用者当前进度
                ProgressChanged?.Invoke(Spliter.CurrentChunkIndex + 1, Spliter.ChunkCount);
            }
            Databuffer.CompleteAdding();

            WorkerStatus = WorkerStatusEnum.Completed;
        }
    }
}

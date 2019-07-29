using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace InfluxStreamSharp.Influx
{
    /// <summary>
    /// 加载并缓冲InfluxDB数据的类
    /// 外部调用顺序：
    /// 1.构造函数，传入起止时间、查询语句模板
    /// 2.调用 Init()
    /// 3.取数据 Dequeue()，传入当前播放时间
    /// 4.调用 LoadDataIfNeeded()，按需加载数据
    /// 5.返回步骤3，直到播放结束
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class QueryWorker<T> : IQueryWorker
    {
        static Logger _logger = new Logger("QueryWorker");

        private DateTime TimeBegin;
        private DateTime TimeEnd;
        private InfluxQLTemplet InfluxQL;
        private TimeSpliter Spliter;
        private InfluxService InfluxDB;
        private ConcurrentQueue<InfluxQueryItem<T>> BufferedQueue = new ConcurrentQueue<InfluxQueryItem<T>>();

        private object _loadLockObj = new object();

        private bool _isLoadingData = false;
        /// <summary>
        /// 是否正在加载数据
        /// </summary>
        public bool IsLoadingData { get => _isLoadingData; set => _isLoadingData = value; }


        public QueryWorker(DateTime timeBegin, DateTime timeEnd, InfluxQLTemplet influxQL)
        {
            TimeBegin = timeBegin;
            TimeEnd = timeEnd;
            InfluxQL = influxQL;
            Spliter = new TimeSpliter(TimeBegin, TimeEnd);
            InfluxDB = InfluxService.Instance.Value;
        }

        #region 已经加载的数据状态
        /// <summary>
        /// 缓冲区内的结束时间
        /// </summary>
        private DateTime _cachedTimeEnd;

        #endregion

        public async Task Init()
        {
            //首次使用先加载一遍数据
            await LoadDataAsync();
        }

        /// <summary>
        /// 加载下一时间片内的数据
        /// </summary>
        /// <returns>True：未到结尾，False：到达结尾</returns>
        private async Task<bool> LoadDataAsync()
        {
            return await Task.Run(() => { return LoadData(); });
        }

        /// <summary>
        /// 加载下一时间片内的数据
        /// </summary>
        /// <returns>True：未到结尾，False：到达结尾</returns>
        private bool LoadData()
        {
            bool notReachTheEnd = false;
            lock (_loadLockObj)
            {
                IsLoadingData = true;

                if (Spliter.NextChunk(out DateTime buffTimeBegin, out DateTime buffTimeEnd))
                {
                    //记录当前缓冲的截止时间
                    _cachedTimeEnd = buffTimeEnd;
                    //生成当前分区的查询语句
                    InfluxQL.LocalBeginTime = buffTimeBegin;
                    InfluxQL.LocalEndTime = buffTimeEnd;
                    string influxQL = InfluxQL.GetInfluxQL();

                    _logger.LogInfo("开始加载Influx数据，IQL：" + influxQL);

                    //加载当前分区的数据
                    List<InfluxQueryItem<T>> result = InfluxDB.Query<T>(influxQL).Result;
                    if (result != null)
                    {
                        //当前分区数据入队
                        foreach (var data in result)
                        {
                            BufferedQueue.Enqueue(data);
                        }
                    }
                    notReachTheEnd = true;
                }
                notReachTheEnd = false;

                IsLoadingData = false;
            }
            return notReachTheEnd;
        }

        /// <summary>
        /// 取出小于传入时间的数据
        /// </summary>
        /// <param name="currentPlayTime"></param>
        /// <returns></returns>
        public IEnumerable Dequeue(DateTime currentPlayTime)
        {
            while (true)
            {
                if (BufferedQueue.TryPeek(out InfluxQueryItem<T> data))
                {
                    if (data.LocalTime <= currentPlayTime)
                    {
                        if (BufferedQueue.TryDequeue(out InfluxQueryItem<T> data2))
                        {
                            if (data != data2)
                            {
                                //出错，可能队列有多个消费者
                                _logger.LogError("此对象线程不安全，请检查是否有多个线程在同时取数据");
                            }
                        }
                        Console.Write($"dataTime: {data.LocalTime.ToString("yyyy-MM-dd HH:mm:ss")} ");
                        yield return data.Data;
                    }
                    else
                    {
                        yield break;
                    }
                }
                else
                {
                    yield break;
                }
            }
            //List<T> list = new List<T>();
            //while (true)
            //{
            //    if (BufferedQueue.TryPeek(out InfluxQueryItem<T> data))
            //    {
            //        if (data.LocalTime <= currentPlayTime)
            //        {
            //            if (BufferedQueue.TryDequeue(out InfluxQueryItem<T> data2))
            //            {
            //                if (data != data2)
            //                {
            //                    //出错，可能队列有多个消费者
            //                    _logger.LogError("此对象线程不安全，请检查是否有多个线程在同时取数据");
            //                }
            //                list.Add(data.Data);
            //            }
            //        }
            //        else
            //        {
            //            break;
            //        }
            //    }
            //    else
            //    {
            //        break;
            //    }
            //}
            //return list;
        }

        /// <summary>
        /// 取出小于传入时间的数据
        /// </summary>
        /// <param name="currentPlayTime"></param>
        /// <returns></returns>
        public IEnumerable<T> SafeDequeue(DateTime currentPlayTime)
        {
            foreach (var data in Dequeue(currentPlayTime))
            {
                yield return (T)data;
            }
        }

        /// <summary>
        /// 判断是否需要加载数据
        /// </summary>
        /// <param name="currentPlayTime"></param>
        /// <returns></returns>
        public bool IfNeedLoad(DateTime currentPlayTime)
        {
            if (Spliter.CurrentChunkIndex < Spliter.ChunkCount)
            {
                return (_cachedTimeEnd - currentPlayTime).TotalMinutes <= (Spliter.ChunkLength / 2);
            }
            return false;
        }

        /// <summary>
        /// 判断是否需要加载数据
        /// 如果需要则自动加载
        /// </summary>
        /// <param name="currentPlayTime"></param>
        /// <returns></returns>
        public async Task<bool> LoadDataIfNeeded(DateTime currentPlayTime)
        {
            if (IfNeedLoad(currentPlayTime))
            {
                //如果已缓冲的数据持续时间小于一次性缓冲时长的一半，则需要加载数据
                _logger.LogInfo($"CurrentPlayTime: {currentPlayTime.ToString("yyyy-MM-dd HH:mm:ss")}, 需要预加载数据");
                return await LoadDataAsync();
            }
            return false;
        }
    }
}

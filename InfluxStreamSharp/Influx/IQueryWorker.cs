using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace InfluxStreamSharp.Influx
{
    public interface IQueryWorker
    {
        Task Init();

        /// <summary>
        /// 取出小于传入时间的数据
        /// </summary>
        /// <param name="currentPlayTime"></param>
        /// <returns></returns>
        IEnumerable Dequeue(DateTime currentPlayTime);

        /// <summary>
        /// 判断是否需要加载数据
        /// 如果需要则自动加载
        /// </summary>
        /// <param name="currentPlayTime"></param>
        /// <returns></returns>
        Task<bool> LoadDataIfNeeded(DateTime currentPlayTime);

        bool IfNeedLoad(DateTime currentPlayTime);

        bool IsLoadingData { get; set; }
    }
}

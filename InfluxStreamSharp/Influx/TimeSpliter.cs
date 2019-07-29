using System;
using System.Collections.Generic;
using System.Text;

namespace InfluxStreamSharp.Influx
{
    /// <summary>
    /// 可以时间分区的类
    /// 用于在Influx查询时对时间段进行分割
    /// </summary>
    public class TimeSpliter
    {
        /// <summary>
        /// 开始时间
        /// </summary>
        private DateTime TimeBegin;

        /// <summary>
        /// 结束时间
        /// </summary>
        private DateTime TimeEnd;

        /// <summary>
        /// 分区的最大长度，单位：分钟
        /// </summary>
        public int ChunkLength { get; private set; }

        /// <summary>
        /// 当前分区的索引
        /// </summary>
        public int CurrentChunkIndex { get; set; } = 0;

        /// <summary>
        /// 总分区数
        /// </summary>
        public int ChunkCount { get; private set; }

        /// <summary>
        /// 初始化一个时间分区类
        /// </summary>
        /// <param name="TimeBegin">开始时间</param>
        /// <param name="TimeEnd">结束时间</param>
        /// <param name="StepLength">分区的最大长度，单位：分钟</param>
        public TimeSpliter(DateTime TimeBegin, DateTime TimeEnd, int ChunkLength = 10)
        {
            if (TimeEnd <= TimeBegin)
            {
                throw new Exception("结束时间必须大于开始时间");
            }

            this.TimeBegin = TimeBegin;
            this.TimeEnd = TimeEnd;
            if (ChunkLength <=1)
            {
                ChunkLength = 10;
            }
            this.ChunkLength = ChunkLength;

            Init();
        }

        private void Init()
        {
            CurrentChunkIndex = 0;

            //计算总分区数
            int totalMinutes = (int)((TimeEnd - TimeBegin).TotalMinutes);
            int chunkCount = totalMinutes / ChunkLength;
            if (totalMinutes % ChunkLength != 0) chunkCount++;

            ChunkCount = chunkCount;
        }

        /// <summary>
        /// 获取当前分区的起止时间
        /// 并且指针向下一分区移动
        /// </summary>
        /// <param name="chunkBegin"></param>
        /// <param name="chunkEnd"></param>
        /// <returns>是否可以继续移动指针，True：未到达结尾，False：到达结尾</returns>
        public bool NextChunk(out DateTime chunkBegin, out DateTime chunkEnd)
        {
            chunkBegin = TimeBegin.AddMinutes(CurrentChunkIndex * ChunkLength);
            chunkEnd = chunkBegin.AddMinutes(ChunkLength);
            if (chunkEnd > TimeEnd)
            {
                chunkEnd = TimeEnd;
            }

            if (CurrentChunkIndex < ChunkCount)
            {
                CurrentChunkIndex++;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}

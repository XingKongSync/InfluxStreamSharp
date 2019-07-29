using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InfluxStreamSharp.DataModel;

namespace InfluxStreamSharp.Influx
{
    /// <summary>
    /// 用于生成Influx查询语句的类
    /// 注意：本类线程不安全
    /// </summary>
    public class InfluxQLTemplet
    {
        /// <summary>
        /// 待查询的表名
        /// </summary>
        public string Measurement;

        /// <summary>
        /// 查询开始时间
        /// </summary>
        public DateTime LocalBeginTime;

        /// <summary>
        /// 查询结束时间
        /// </summary>
        public DateTime LocalEndTime;

        private List<string> _whereParts;
        /// <summary>
        /// 查询条件的集合
        /// </summary>
        private List<string> WhereParts { get => _whereParts = _whereParts ?? new List<string>(); }

        /// <summary>
        /// 添加一个查询条件
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void WhereEqual(string key, string value)
        {
            WhereParts.Add($"\"{key}\" = '{value}'");
        }

        /// <summary>
        /// 生成Influx查询语句
        /// </summary>
        /// <returns></returns>
        public string GetInfluxQL()
        {
            var args = WhereParts.ToList();
            if (LocalBeginTime != default(DateTime))
            {
                long timestamp = DateTimeConverter.LocalTimeToTimestamp(LocalBeginTime);
                args.Add($"time > {timestamp}000000");
            }
            if (LocalEndTime != default(DateTime))
            {
                long timestamp = DateTimeConverter.LocalTimeToTimestamp(LocalEndTime);
                args.Add($"time <= {timestamp}000000");
            }
            StringBuilder sb = new StringBuilder();
            sb.Append($"select * from {Measurement} ");

            if (args.Count > 0)
            {
                sb.Append("WHERE ");
                string whereStatement = string.Join(" AND ", args);
                sb.Append(whereStatement);
            }

            return sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace InfluxStreamSharp.DataModel
{
    public enum InfluxFieldType
    {
        /// <summary>
        /// 值
        /// </summary>
        Value,

        /// <summary>
        /// 索引标签
        /// </summary>
        Tag,

        /// <summary>
        /// 时间戳字段
        /// </summary>
        Timestamp,

        /// <summary>
        /// 忽略该字段
        /// </summary>
        Ignore
    }

    /// <summary>
    /// 用于指示InfluxDB字段与接口字段对应关系的特性
    /// 字段分为两种：Tag和Value
    /// 也可以用于指示待写入的Influx表名（Measurement）
    /// </summary>
    public class InfluxModelAttribute : Attribute
    {
        public InfluxFieldType FieldType;
        public string Measurement;

        public InfluxModelAttribute(InfluxFieldType fieldType)
        {
            FieldType = fieldType;
        }

        public InfluxModelAttribute(string measurement)
        {
            Measurement = measurement;
        }
    }
}

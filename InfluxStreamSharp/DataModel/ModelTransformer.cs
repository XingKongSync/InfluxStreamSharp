using AdysTech.InfluxDB.Client.Net;
using LogManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace InfluxStreamSharp.DataModel
{
    public static class ModelTransformer
    {
        private static LogService _logger = LogService.Instance.Value;

        /// <summary>
        /// 解析模型对应的Influx表名（Measurement）
        /// </summary>
        /// <param name="modelType"></param>
        /// <returns></returns>
        public static string GetMeasurement(Type modelType)
        {
            InfluxModelAttribute modelAttr = modelType.GetCustomAttribute<InfluxModelAttribute>();
            string measurement = modelAttr?.Measurement;
            return measurement;
        }

        public static InfluxDatapoint<InfluxValueField> Convert<T>(T model)
        {
            if (model == null)
            {
                _logger.LogError($"警告！Model不能为null");
                return null;
            }

            Type t = model.GetType();

            //解析模型对应的Influx表名（Measurement）
            string measurement = GetMeasurement(t);

            if (string.IsNullOrWhiteSpace(measurement))
            {
                _logger.LogError($"警告！Model缺少 InfluxModelAttribute.Measurement");
                return null;
            }

            //解析模型对应的Influx字段
            InfluxDatapoint<InfluxValueField> influxValue = new InfluxDatapoint<InfluxValueField>();
            influxValue.MeasurementName = measurement;

            FieldInfo[] fields = t.GetFields();
            PropertyInfo[] props = t.GetProperties();
            List<MemberInfo> members = new List<MemberInfo>();
            members.AddRange(fields);
            members.AddRange(props);
            foreach (MemberInfo member in members)
            {
                InfluxModelAttribute attr = member.GetCustomAttribute<InfluxModelAttribute>();
                if (attr == null || attr.FieldType == InfluxFieldType.Ignore)
                {
                    continue;
                }
                //Console.WriteLine($"Name: {field.Name}, Data: {field.GetValue(model)}, Type: {attr.FieldType}");
                string name = member.Name;
                object value = null;
                if (member is FieldInfo fInfo)
                {
                    value = fInfo.GetValue(model);
                }
                else if (member is PropertyInfo pInfo)
                {
                    value = pInfo.GetValue(model);
                }
                switch (attr.FieldType)
                {
                    case InfluxFieldType.Value:
                        {
                            IComparable val = value as IComparable;
                            if (val == null)
                            {
                                //收到的字段里可能有null，忽略值为null的字段
                                //_logger.LogError($"警告！Model: {model.GetType().Name}, Field: {name} 转换为Influx数据格式失败");
                                continue;
                            }
                            influxValue.Fields.Add(name, new InfluxValueField(val));
                        }
                        break;
                    case InfluxFieldType.Tag:
                        {
                            string tag = value as string;
                            if (tag == null) tag = value.ToString();
                            influxValue.Tags.Add(name, tag);
                        }
                        break;
                    case InfluxFieldType.Timestamp:
                        {
                            DateTime utcTime = default(DateTime);
                            bool convertSuccess = false;
                            if (value is DateTime dtVal)
                            {
                                utcTime = DateTimeConverter.ToUtcDateTime(dtVal);
                                convertSuccess = true;
                            }
                            else if (value is long lgVal)
                            {
                                utcTime = DateTimeConverter.ToUtcDateTime(lgVal);
                                convertSuccess = true;
                            }
                            if (convertSuccess)
                            {
                                influxValue.UtcTimestamp = utcTime;
                            }
                            else
                            {
                                _logger.LogError($"警告！Model: {t.Name}, Field: {name} 转换为UTC时间失败");
                                continue;
                            }
                        }
                        break;
                    case InfluxFieldType.Ignore:
                        break;
                    default:
                        break;
                }
            }

            return influxValue;
        }
    }
}

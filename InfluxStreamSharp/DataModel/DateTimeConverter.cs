using System;
using System.Collections.Generic;
using System.Text;

namespace InfluxStreamSharp.DataModel
{
    public static class DateTimeConverter
    {
        private static readonly DateTime start = new DateTime(1970, 1, 1);

        /// <summary>
        /// 将时间戳转换为UTC时间的DateTime
        /// </summary>
        /// <param name="timeStamp">时间戳（毫秒）</param>
        /// <returns></returns>
        public static DateTime ToUtcDateTime(long timeStamp)
        {
            return start.AddMilliseconds(timeStamp);
        }

        /// <summary>
        /// 将本地时区的DateTime转换为UTC时间的DateTime
        /// </summary>
        /// <param name="localTime"></param>
        /// <returns></returns>
        public static DateTime ToUtcDateTime(DateTime localTime)
        {
            return TimeZoneInfo.ConvertTime(localTime, TimeZoneInfo.Local, TimeZoneInfo.Utc);
        }

        /// <summary>
        /// 将时间戳转换为本地时间的DateTime
        /// </summary>
        /// <param name="timeStamp"></param>
        /// <returns></returns>
        public static DateTime ToLocalTime(long timeStamp)
        {
            DateTime utcNow = ToUtcDateTime(timeStamp);
            return ToLocalTime(utcNow);
        }

        /// <summary>
        /// 将UTC DateTime转换为本地时间
        /// </summary>
        /// <param name="utcTime"></param>
        /// <returns></returns>
        public static DateTime ToLocalTime(DateTime utcTime)
        {
            return TimeZoneInfo.ConvertTime(utcTime, TimeZoneInfo.Utc, TimeZoneInfo.Local);
        }

        /// <summary>
        /// 将UTC时间的DateTime转换为时间戳
        /// </summary>
        /// <param name="utcTime"></param>
        /// <returns></returns>
        public static long UtcTimeToTimestamp(DateTime utcTime)
        {
            return (long)((utcTime - start).TotalMilliseconds);
        }

        /// <summary>
        /// 将本地时间的DateTime转换为时间戳
        /// </summary>
        /// <param name="localTime"></param>
        /// <returns></returns>
        public static long LocalTimeToTimestamp(DateTime localTime)
        {
            DateTime utcTime = ToUtcDateTime(localTime);
            return UtcTimeToTimestamp(utcTime);
        }
    }
}

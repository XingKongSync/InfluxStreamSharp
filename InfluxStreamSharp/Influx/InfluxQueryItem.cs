using System;
using System.Collections.Generic;
using System.Text;

namespace InfluxStreamSharp.Influx
{
    public class InfluxQueryItem<T>
    {
        public DateTime LocalTime;
        public T Data;
    }
}

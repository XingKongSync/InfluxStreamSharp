using InfluxStreamSharp.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Demo.DataModel
{
    [InfluxModel("test")]
    public class Test
    {
        [InfluxModel(InfluxFieldType.Tag)]
        public string DeviceId;
        [InfluxModel(InfluxFieldType.Value)]
        public double x;
        [InfluxModel(InfluxFieldType.Value)]
        public double y;
        [InfluxModel(InfluxFieldType.Timestamp)]
        public DateTime LocalTime;
    }
}

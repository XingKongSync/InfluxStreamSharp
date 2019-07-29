using AdysTech.InfluxDB.Client.Net;
using InfluxStreamSharp.DataModel;
using InfluxStreamSharp.Influx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Demo
{
    class Program
    {
        private static readonly string DB_Url = @"http://127.0.0.1:8086";
        private static readonly string DB_UserName = "";
        private static readonly string DB_Pwd = "";
        private static readonly string DB_DbName = "MyDatabase";
        private static readonly int DB_RetentionHours = 24 * 30;//Keep data for a month

        static void Main(string[] args)
        {
            //Init a InfluxDB writing buffer
            WriteService.Instance.Value.Start();

            //Create a database if not exist
            var influx = InfluxService.Instance.Value;
            influx.InitAsync(
                DB_Url,
                DB_UserName,
                DB_Pwd,
                DB_DbName,
                DB_RetentionHours
                ).Wait();

            //write data with buffering
            TestStreamingWrite();
            //Read all data by buffering and timing
            TestStreamingRead();

            Console.ReadKey();
        }

        static void TestStreamingWrite()
        {
            for (int i = 0; i < 10; i++)
            {
                var testModel = new DataModel.Test();
                testModel.DeviceId = i.ToString();
                testModel.x = i;
                testModel.y = i;
                testModel.LocalTime = DateTime.Now.AddMinutes(-1 * i);

                //Convert custom data model to influx model
                var point = ModelTransformer.Convert(testModel);
                //Send the data to the writing queue, the data will be buffered and send to InfluxDB
                WriteService.Instance.Value.Enqueue(point);
            }
        }

        static void TestStreamingRead()
        {
            //Build a query statement
            InfluxQLTemplet templet = new InfluxQLTemplet();
            templet.Measurement = ModelTransformer.GetMeasurement(typeof(DataModel.Test));
            //Add query reqirement
            //templet.WhereEqual("DeviceId", "0");//Only query data which DeviceId equals to 0

            //Construct query manager for streaming read
            QueryManager manager = new QueryManager(DateTime.Now.AddMinutes(-30), DateTime.Now);//Query data within 30 miniutes
            //If you want do muliti queries, please add more QueryTemplet
            manager.AddInfluxQueryTemplet<DataModel.Test>(templet);
            //Handle receveied data
            manager.DataReceived += (object data) =>
            {
                if (data is DataModel.Test t)
                {
                    Console.WriteLine($"CurrentPlayTime: {manager.CurrentPlayTime.ToString("yyyy-MM-dd HH:mm:ss")}, id: {t.DeviceId}, x: {t.x}, y: {t.y}");
                }
            };
            //Start query data
            manager.Start();
        }
    }
}

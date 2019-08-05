using AdysTech.InfluxDB.Client.Net;
using LogManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace InfluxStreamSharp.Influx
{
    public class InfluxService
    {
        public static readonly Lazy<InfluxService> Instance = new Lazy<InfluxService>(() => new InfluxService());

        private Logger _logger;
        private bool _hasInited = false;
        private InfluxDBClient influxClient;

        #region 配置参数
        private string _influxUrl;
        private string _influxUName;
        private string _influxUPwd;
        private string _influxDbName;
        private long _influxRetentionHours;
        #endregion

        private InfluxService() { _logger = new Logger("InfluxService"); }

        public async Task InitAsync(string influxUrl, string influxUName, string influxUPwd, string influxDbName, long influxRetentionHours)
        {
            if (_hasInited) return;
            _hasInited = true;

            //保存初始化的参数
            _influxUrl = influxUrl;
            _influxUName = influxUName;
            _influxUPwd = influxUPwd;
            _influxDbName = influxDbName;
            _influxRetentionHours = influxRetentionHours;
            try
            {
                influxClient = new InfluxDBClient(influxUrl, influxUName, influxUPwd);

                //检查数据库是否存在，如果数据库不存在，则新建数据库，并且新建数据保留策略
                await CreateDatabaseAndRetentionPolicyAsync(influxDbName);
            }
            catch (Exception ex)
            {
                _logger.LogError("初始化InfluxDB失败，原因：" + ex.Message);
                _logger.LogError("程序即将退出...");
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// 创建数据库和对应的数据保留策略
        /// </summary>
        /// <param name="dbName"></param>
        private async Task CreateDatabaseAndRetentionPolicyAsync(string dbName)
        {
            //新建数据库，如果数据库已存在也会成功
            _logger.LogDebug($"检查数据库 {dbName}");
            await influxClient.CreateDatabaseAsync(dbName);

            //新建保留策略
            string rpName = GenerateRpName(dbName);//生成约定的保留策略名称
            _logger.LogDebug($"检查保留策略 {rpName}");
            var rp = new InfluxRetentionPolicy()
            {
                Name = rpName,
                DBName = dbName,
                Duration = TimeSpan.FromHours(_influxRetentionHours),
                IsDefault = true
            };
            if (!await influxClient.CreateRetentionPolicyAsync(rp))
            {
                throw new InvalidOperationException($"为 {dbName} 创建保留策略失败，策略名称：{rpName}，保留时长：{_influxRetentionHours}小时");
            }
        }

        /// <summary>
        /// 根据数据库名称生成约定的保留策略名称
        /// </summary>
        /// <param name="dbName"></param>
        /// <returns></returns>
        private string GenerateRpName(string dbName)
        {
            return $"{dbName}_rp";
        }

        private DateTime _lastPrintErrTime = DateTime.Now.AddHours(-1);//记录一下上次出错后打印日志的时间
        /// <summary>
        /// 写入数据集合
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public async Task WriteAsync(List<InfluxDatapoint<InfluxValueField>> points)
        {
            try
            {
                //TODO: 添加错误log，通常错误是由于字段类型改变引起的
                await influxClient.PostPointsAsync(_influxDbName, points);
            }
            catch (Exception ex)
            {
                if (_lastPrintErrTime.AddMinutes(1) < DateTime.Now)
                {
                    //1分钟之内重复收到消息只打1次日志
                    _lastPrintErrTime = DateTime.Now;
                    _logger.LogError("写入InfluxDB出错，原因：" + ex.Message + "\r\n" + ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// 执行Influx查询语句并转换结果为指定的实体类型
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="influxQL">查询语句</param>
        /// <returns>转换后的实体集合</returns>
        public async Task<List<InfluxQueryItem<T>>> Query<T>(string influxQL)
        {
            try
            {
                List<InfluxQueryItem<T>> queryResult = new List<InfluxQueryItem<T>>();
                Stopwatch watch = new Stopwatch();
                watch.Start();
                //执行查询语句
                var result = await influxClient.QueryMultiSeriesAsync(_influxDbName, influxQL);
                if (result != null)
                {
                    //InfluxDB支持一次性执行多条查询语句，但是本函数支持传递单条查询语句
                    foreach (var series in result)
                    {
                        if (series.HasEntries)
                        {
                            foreach (var data in series.Entries)
                            {
                                //转换为指定的实体类型，采用json格式进行中转，可以思考有没有更加高效的转换方法
                                string tempJson = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                                T obj = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(tempJson);

                                //由于直接转换为实体类会丢失时间戳，所以通过 InfluxQueryItem 来保留时间戳
                                InfluxQueryItem<T> queryItem = new InfluxQueryItem<T>();
                                queryItem.LocalTime = DataModel.DateTimeConverter.ToLocalTime((DateTime)data.Time);
                                queryItem.Data = obj;
                                queryResult.Add(queryItem);
                            }
                        }
                    }
                }
                watch.Stop();
                _logger.LogDebug($"查询耗时：{watch.Elapsed.TotalSeconds}s，InfluxQL：{influxQL}");
                return queryResult;
            }
            catch (Exception ex)
            {
                _logger.LogError($"查询出错，InfluxQL：{influxQL}，原因：{ex.Message}\r\n{ex.StackTrace}");
            }
            return null;
        }
    }
}

# InfluxStreamSharp
A InfluxDB query and writing library

## 具有写入缓存队列
适合于持续性的数据写入操作
## 数据模板
借鉴 Entity Framework 的思路，通过构建实体对象到数据库的映射来读取和写入数据
## 支持根据时间范围进行流式读取
通过构建查询模板，对指定时间范围内的数据分块查询，不会一次性将数据全部载入到内存，并且会按照数据原有的写入间隔将数据返回给上层，重新还原写入时的场景，就好像在播放视频一样！

## Usage / 代码示例
```C#
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
```

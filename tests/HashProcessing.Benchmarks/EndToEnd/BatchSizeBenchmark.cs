using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using HashProcessing.Api.Infrastructure;
using HashProcessing.Benchmarks.Infrastructure;
using HashProcessing.Worker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HashProcessing.Benchmarks.EndToEnd;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 3)]
public class BatchSizeBenchmark
{
    private const uint HashCount = 40_000;
    private const string HashProcessingQueue = "hash-processing";
    private const string DailyCountsQueue = "hash-daily-counts";

    private static readonly Dictionary<string, object?> QueueArguments = new()
    {
        ["x-dead-letter-exchange"] = "dlx"
    };

    private RabbitMqFixture _rabbitMq = null!;
    private MariaDbFixture _mariaDb = null!;
    private BenchmarkWorkerFactory _workerFactory = null!;
    private BenchmarkApiFactory _apiFactory = null!;
    private HttpClient _httpClient = null!;
    private CompletionSignal _completionSignal = null!;

    [Params(50, 100, 250, 500, 1_000)]
    public ushort BatchSize { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _rabbitMq = new RabbitMqFixture();
        _mariaDb = new MariaDbFixture();
        await Task.WhenAll(_rabbitMq.StartAsync(), _mariaDb.StartAsync());

        _completionSignal = new CompletionSignal();

        _workerFactory = new BenchmarkWorkerFactory(_rabbitMq, _mariaDb);
        await _workerFactory.StartAsync();

        _apiFactory = new BenchmarkApiFactory(_rabbitMq, _mariaDb, _completionSignal, BatchSize);
        _httpClient = _apiFactory.CreateClient();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _rabbitMq.PurgeQueueAsync(HashProcessingQueue, QueueArguments).GetAwaiter().GetResult();
        _rabbitMq.PurgeQueueAsync(DailyCountsQueue, QueueArguments).GetAwaiter().GetResult();
        TruncateTablesAsync().GetAwaiter().GetResult();

        var expectedMessages = (int)Math.Ceiling((double)HashCount / BatchSize);
        _completionSignal.Reset(expectedMessages);
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        _httpClient.Dispose();
        await _apiFactory.DisposeAsync();
        await _workerFactory.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await _mariaDb.DisposeAsync();
    }

    [Benchmark]
    public async Task FullRoundtrip()
    {
        var response = await _httpClient.PostAsync($"/hashes?count={HashCount}", null);
        response.EnsureSuccessStatusCode();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await _completionSignal.WaitAsync(cts.Token);
    }

    private async Task TruncateTablesAsync()
    {
        using (var scope = _workerFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HashDbContext>();
            await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE hashes");
        }

        using (var scope = _apiFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE hash_daily_counts");
        }
    }
}

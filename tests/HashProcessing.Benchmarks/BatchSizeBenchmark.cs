using BenchmarkDotNet.Attributes;
using HashProcessing.Api.Core;
using HashProcessing.Api.Infrastructure;
using HashProcessing.Benchmarks.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace HashProcessing.Benchmarks;

[MemoryDiagnoser]
public class BatchSizeBenchmark
{
    private const ushort DegreeOfParallelism = 0; // ProcessorCount
    private const uint HashCount = 1_000_000;
    private const string QueueName = "benchmark-batch-size-hash-processing";

    private RabbitMqFixture _fixture = null!;

    [Params(10, 50, 100, 250, 500, 1_000, 2_000, 5_000, 10_000, 20_000, 40_000)]
    public ushort BatchSize { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _fixture = new RabbitMqFixture();
        await _fixture.StartAsync();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _fixture.PurgeQueueAsync(QueueName).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await _fixture.DisposeAsync();
    }

    [Benchmark]
    public async Task<ProcessResult> Default_GenerateAndPublish()
    {
        var generator = new DefaultHashGenerator(128);
        var reader = generator.StreamSha1s(HashCount);

        var processor = new RabbitMqBatchedOffloadToWorkerProcessor(
            _fixture.ConnectionFactory,
            NullLoggerFactory.Instance,
            DegreeOfParallelism,
            BatchSize,
            QueueName);

        return await processor.ProcessAsync(reader);
    }
}

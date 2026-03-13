using BenchmarkDotNet.Attributes;
using HashProcessing.Api.Core;
using HashProcessing.Api.Infrastructure;
using HashProcessing.Benchmarks.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace HashProcessing.Benchmarks;

[MemoryDiagnoser]
public class ParallelDegreeOfParallelismBenchmark
{
    private const ushort BatchSize = 100;
    private const uint HashCount = 40_000;
    private const string QueueName = "benchmark-dop-hash-processing";

    private RabbitMqFixture _fixture = null!;

    [Params(1, 2, 4, 8, 0)]
    public ushort DegreeOfParallelism { get; set; }

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
    public async Task<ProcessResult> Parallel_GenerateAndPublish()
    {
        var generator = new ParallelHashGenerator(DegreeOfParallelism, null);
        var reader = generator.StreamSha1s(HashCount);

        var processor = new RabbitMqBatchedOffloadToWorkerProcessor(
            _fixture.Connection,
            NullLoggerFactory.Instance,
            DegreeOfParallelism,
            BatchSize,
            QueueName);

        return await processor.ProcessAsync(reader);
    }
}

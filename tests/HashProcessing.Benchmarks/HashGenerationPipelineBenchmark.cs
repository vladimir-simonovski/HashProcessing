using BenchmarkDotNet.Attributes;
using HashProcessing.Api.Core;
using HashProcessing.Api.Infrastructure;
using HashProcessing.Benchmarks.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace HashProcessing.Benchmarks;

[MemoryDiagnoser]
public class HashGenerationPipelineBenchmark
{
    private const ushort BatchSize = 500;
    private const ushort ChannelCapacity = 128;
    private const ushort DegreeOfParallelism = 0; // ProcessorCount
    private const string QueueName = "benchmark-hash-processing";

    private RabbitMqFixture _fixture = null!;
    private IHashProcessor _processor = null!;

    [Params(1_000, 10_000, 40_000, 100_000)]
    public uint Count { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _fixture = new RabbitMqFixture();
        await _fixture.StartAsync();

        _processor = new RabbitMqBatchedOffloadToWorkerProcessor(
            _fixture.ConnectionFactory,
            NullLoggerFactory.Instance,
            DegreeOfParallelism,
            BatchSize,
            QueueName);
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

    [Benchmark(Baseline = true)]
    public async Task<ProcessResult> Default_GenerateAndPublish()
    {
        var generator = new DefaultHashGenerator(ChannelCapacity);
        var reader = generator.StreamSha1s(Count);
        return await _processor.ProcessAsync(reader);
    }

    [Benchmark]
    public async Task<ProcessResult> Parallel_GenerateAndPublish()
    {
        var generator = new ParallelHashGenerator(DegreeOfParallelism, null);
        var reader = generator.StreamSha1s(Count);
        return await _processor.ProcessAsync(reader);
    }
}

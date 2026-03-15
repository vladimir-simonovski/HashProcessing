using BenchmarkDotNet.Attributes;
using HashProcessing.Api.Core;
using HashProcessing.Api.Infrastructure;
using HashProcessing.Benchmarks.Infrastructure;
using HashProcessing.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HashProcessing.Benchmarks.Producer;

[MemoryDiagnoser]
public class HashGenerationPipelineBenchmark
{
    private const ushort BatchSize = 500;
    private const ushort DegreeOfParallelism = 0; // ProcessorCount
    private const string QueueName = "benchmark-hash-processing";

    private static readonly QueueArguments QueueArguments = new()
    {
        DeadLetterExchange = "dlx"
    };

    private RabbitMqFixture _fixture = null!;
    private IHashProcessor _processor = null!;

    [Params(1_000, 10_000, 40_000, 100_000)]
    public uint Count { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _fixture = new RabbitMqFixture();
        await _fixture.StartAsync();

        var options = Options.Create(new HashProcessingOptions
        {
            DegreeOfParallelism = DegreeOfParallelism,
            BatchSize = BatchSize,
            PublishQueueName = QueueName
        });

        var publisherPool = new PublisherChannelPool(
            _fixture.Connection,
            NullLoggerFactory.Instance.CreateLogger<PublisherChannelPool>());

        var publisher = new RabbitMqPublisher(
            publisherPool,
            NullLoggerFactory.Instance.CreateLogger<RabbitMqPublisher>());

        _processor = new RabbitMqBatchedOffloadToWorkerProcessor(
            publisher,
            options);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _fixture.PurgeQueueAsync(QueueName, QueueArguments).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await _fixture.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public async Task<ProcessResult> Default_GenerateAndPublish()
    {
        var generator = new DefaultHashGenerator(Options.Create(new HashProcessingOptions()));
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

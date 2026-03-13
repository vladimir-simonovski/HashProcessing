using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using HashProcessing.Benchmarks.Infrastructure;
using HashProcessing.Messaging;
using HashProcessing.Worker.Application;
using HashProcessing.Worker.Core;
using HashProcessing.Worker.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace HashProcessing.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 3)]
public class PrefetchCountBenchmark
{
    private const int MessageCount = 20_000;
    private const int HashesPerMessage = 10;
    private const int ConsumerCount = 4;
    private const string ConsumeQueueName = "benchmark-prefetch-hash-processing";
    private const string PublishQueueName = "benchmark-prefetch-hash-daily-counts";

    private RabbitMqFixture _fixture = null!;
    private CountingHashRepository _countingRepository = null!;
    private RabbitMqHashConsumer _consumer = null!;
    private IConnection _preloadConnection = null!;

    [Params(1, 10, 25, 50, 250)]
    public ushort PrefetchCount { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _fixture = new RabbitMqFixture();
        await _fixture.StartAsync();
        _preloadConnection = await _fixture.ConnectionFactory.CreateConnectionAsync();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _fixture.PurgeQueueAsync(ConsumeQueueName).GetAwaiter().GetResult();
        _fixture.PurgeQueueAsync(PublishQueueName).GetAwaiter().GetResult();

        PreloadMessagesAsync().GetAwaiter().GetResult();

        _countingRepository = new CountingHashRepository();
        _countingRepository.Reset(MessageCount);

        var services = new ServiceCollection();
        services.AddSingleton(_fixture.ConnectionFactory);
        services.AddSingleton(_preloadConnection);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IHashRepository>(_countingRepository);
        services.AddScoped<ProcessReceivedHashesCommandHandler>();
        services.AddScoped(sp =>
            new RabbitMqPublisher(
                sp.GetRequiredService<IConnection>(),
                sp.GetRequiredService<ILogger<RabbitMqPublisher>>(),
                PublishQueueName));

        var sp = services.BuildServiceProvider();

        _consumer = new RabbitMqHashConsumer(
            _fixture.ConnectionFactory,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RabbitMqHashConsumer>.Instance,
            ConsumeQueueName,
            PrefetchCount);
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        _preloadConnection.Dispose();
        await _fixture.DisposeAsync();
    }

    [Benchmark]
    public async Task ConsumeMessages()
    {
        using var cts = new CancellationTokenSource();

        var consumerTasks = new Task[ConsumerCount];
        for (var i = 0; i < ConsumerCount; i++)
        {
            var id = i;
            consumerTasks[i] = _consumer.ConsumeAsync(id, cts.Token);
        }

        await _countingRepository.WaitAsync(cts.Token);

        await cts.CancelAsync();

        await Task.WhenAll(consumerTasks.Select(t =>
            t.ContinueWith(_ => { }, TaskScheduler.Default)));
    }

    private async Task PreloadMessagesAsync()
    {
        await using var publisher = new RabbitMqPublisher(
            _preloadConnection,
            NullLogger<RabbitMqPublisher>.Instance,
            ConsumeQueueName);

        var date = DateTimeOffset.UtcNow;

        for (var i = 0; i < MessageCount; i++)
        {
            var hashes = new Hash[HashesPerMessage];
            for (var j = 0; j < HashesPerMessage; j++)
            {
                var sha1 = Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();
                hashes[j] = new Hash(Guid.NewGuid().ToString(), date, sha1);
            }

            await publisher.PublishAsync(new HashBatchMessage(hashes));
        }
    }
}

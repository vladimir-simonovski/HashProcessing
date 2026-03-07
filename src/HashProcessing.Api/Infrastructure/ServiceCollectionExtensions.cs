using HashProcessing.Api.Core;
using RabbitMQ.Client;

namespace HashProcessing.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO: Replace hardcoded values with IConfiguration-based options (e.g., IOptions<T> pattern).
        const ushort channelCapacity = 128;
        const ushort degreeOfParallelism = 0; // 0 = Environment.ProcessorCount
        const ushort batchSize = 100;
        const string queueName = "hash-processing";
        var rabbitMqHost = configuration["RabbitMQ:HostName"] ?? "localhost";

        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = rabbitMqHost
        });

        services.AddTransient<IHashGenerator>(_ =>
            new DefaultHashGenerator(channelCapacity));

        services.AddSingleton<IHashProcessor>(sp =>
            new RabbitMqBatchedOffloadToWorkerProcessor(
                sp.GetRequiredService<IConnectionFactory>(),
                degreeOfParallelism,
                batchSize,
                queueName));

        return services;
    }
}
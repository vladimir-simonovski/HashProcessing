using HashProcessing.Messaging;
using HashProcessing.Worker.Core;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace HashProcessing.Worker.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        const string consumeQueueName = "hash-processing";
        const string publishQueueName = "hash-daily-counts";
        const string deadLetterExchange = "dlx";
        var queueArguments = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = deadLetterExchange };
        var rabbitMqHost = configuration["RabbitMQ:HostName"] ?? "localhost";
        var connectionString = configuration.GetConnectionString("MariaDb")
                               ?? "Server=localhost;Database=worker;User=root;Password=root;";

        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = rabbitMqHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        });

        services.AddDbContext<HashDbContext>(options =>
            options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 0))));

        services.AddScoped<IHashRepository, HashRepository>();

        services.AddSingleton(sp =>
            sp.GetRequiredService<IConnectionFactory>().CreateConnectionAsync().GetAwaiter().GetResult());

        services.AddScoped(sp =>
            new RabbitMqPublisher(
                sp.GetRequiredService<IConnection>(),
                sp.GetRequiredService<ILogger<RabbitMqPublisher>>(),
                publishQueueName,
                queueArguments));

        services.AddSingleton(sp =>
            new RabbitMqHashConsumer(
                sp.GetRequiredService<IConnectionFactory>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<RabbitMqHashConsumer>>(),
                consumeQueueName,
                prefetchCount: 10,
                queueArguments: queueArguments));

        return services;
    }
}

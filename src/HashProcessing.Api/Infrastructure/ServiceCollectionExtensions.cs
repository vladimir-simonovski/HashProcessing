using HashProcessing.Api.Core;
using HashProcessing.Messaging;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace HashProcessing.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HashProcessingOptions>(configuration.GetSection("HashProcessing"));

        var rabbitMqHost = configuration["RabbitMQ:HostName"] ?? "localhost";
        var connectionString = configuration.GetConnectionString("MariaDb")
                               ?? "Server=localhost;Database=api;User=root;Password=root;";

        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = rabbitMqHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        });

        services.AddSingleton(sp =>
            sp.GetRequiredService<IConnectionFactory>().CreateConnectionAsync().GetAwaiter().GetResult());

        services.AddSingleton<ConsumerChannelPool>();
        services.AddSingleton<PublisherChannelPool>();

        services.AddSingleton<RabbitMqPublisher>();

        services.AddDbContext<ApiDbContext>(options =>
            options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 0))));

        services.AddScoped<IHashDailyCountRepository, HashDailyCountRepository>();
        services.AddTransient<IHashGenerator, DefaultHashGenerator>();
        services.AddSingleton<IHashProcessor, RabbitMqBatchedOffloadToWorkerProcessor>();
        services.AddSingleton<HashDailyCountEventConsumer>();
        services.AddHostedService<HashDailyCountEventBackgroundService>();

        return services;
    }
}
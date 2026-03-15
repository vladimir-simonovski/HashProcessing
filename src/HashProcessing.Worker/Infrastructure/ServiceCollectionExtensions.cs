using HashProcessing.Messaging;
using HashProcessing.Worker.Application;
using HashProcessing.Worker.Core;
using Microsoft.EntityFrameworkCore;

namespace HashProcessing.Worker.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkerOptions>(configuration.GetSection("Worker"));

        const string consumeQueueName = "hash-processing";
        const string deadLetterExchange = "dlx";
        var queueArguments = new QueueArguments { DeadLetterExchange = deadLetterExchange };
        var rabbitMqHost = configuration["RabbitMQ:HostName"] ?? "localhost";
        var connectionString = configuration.GetConnectionString("MariaDb")
                               ?? "Server=localhost;Database=worker;User=root;Password=root;";

        services.AddRabbitMq(rabbitMqHost);

        services.AddDbContext<HashDbContext>(options =>
            options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 0))));

        services.AddScoped<IHashRepository, HashRepository>();

        services.AddSingleton(sp =>
            new RabbitMqHashConsumer(
                sp.GetRequiredService<ConsumerChannelPool>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<RabbitMqHashConsumer>>(),
                consumeQueueName,
                prefetchCount: 10,
                queueArguments: queueArguments));

        return services;
    }
}

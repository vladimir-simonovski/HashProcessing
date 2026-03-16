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
        var rabbitMqUser = configuration["RabbitMQ:UserName"];
        var rabbitMqPass = configuration["RabbitMQ:Password"];
        var connectionString = configuration.GetConnectionString("MariaDb")
                               ?? throw new InvalidOperationException("Connection string 'MariaDb' is not configured.");

        services.AddRabbitMq(rabbitMqHost, rabbitMqUser, rabbitMqPass);

        services.AddDbContext<HashDbContext>(options =>
            options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 0)),
                mysqlOptions => mysqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

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

using HashProcessing.Messaging;
using HashProcessing.Worker.Application;
using HashProcessing.Worker.Core;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace HashProcessing.Worker.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkerOptions>(configuration.GetSection("Worker"));

        const string consumeQueueName = "hash-processing";
        const string deadLetterExchange = "dlx";
        var queueArguments = new QueueArguments { DeadLetterExchange = deadLetterExchange };
        var rabbitMqHost = configuration["RabbitMQ:HostName"]
                               ?? throw new InvalidOperationException("Configuration 'RabbitMQ:HostName' is not configured.");
        var rabbitMqUser = configuration["RabbitMQ:UserName"]
                               ?? throw new InvalidOperationException("Configuration 'RabbitMQ:UserName' is not configured.");
        var rabbitMqPass = configuration["RabbitMQ:Password"]
                               ?? throw new InvalidOperationException("Configuration 'RabbitMQ:Password' is not configured.");
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
        services.AddScoped<IDailyHashCountNotifier, RabbitMqDailyHashCountNotifier>();

        services.AddSingleton(sp =>
            new RabbitMqHashConsumer(
                sp.GetRequiredService<IConnection>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<RabbitMqHashConsumer>>(),
                consumeQueueName,
                prefetchCount: 10,
                queueArguments: queueArguments));

        return services;
    }
}

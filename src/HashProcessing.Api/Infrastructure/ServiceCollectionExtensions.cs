using HashProcessing.Api.Core;
using HashProcessing.Messaging;
using Microsoft.EntityFrameworkCore;

namespace HashProcessing.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HashProcessingOptions>(configuration.GetSection("HashProcessing"));

        var rabbitMqHost = configuration["RabbitMQ:HostName"] ?? "localhost";
        var rabbitMqUser = configuration["RabbitMQ:UserName"];
        var rabbitMqPass = configuration["RabbitMQ:Password"];
        var connectionString = configuration.GetConnectionString("MariaDb")
                               ?? throw new InvalidOperationException("Connection string 'MariaDb' is not configured.");

        services.AddRabbitMq(rabbitMqHost, rabbitMqUser, rabbitMqPass);

        services.AddDbContext<ApiDbContext>(options =>
            options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 0)),
                mysqlOptions => mysqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

        services.AddScoped<IHashDailyCountRepository, HashDailyCountRepository>();
        services.AddTransient<IHashGenerator, DefaultHashGenerator>();
        services.AddSingleton<IHashProcessor, RabbitMqBatchedOffloadToWorkerProcessor>();
        services.AddSingleton<HashDailyCountEventConsumer>();
        services.AddHostedService<HashDailyCountEventBackgroundService>();

        return services;
    }
}
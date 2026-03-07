using HashProcessing.Worker.Core;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace HashProcessing.Worker.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        const string queueName = "hash-processing";
        var rabbitMqHost = configuration["RabbitMQ:HostName"] ?? "localhost";
        var connectionString = configuration.GetConnectionString("MariaDb")
                               ?? "Server=localhost;Database=hashprocessing;User=root;Password=root;";

        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = rabbitMqHost
        });

        services.AddDbContext<HashDbContext>(options =>
            options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 0))));

        services.AddScoped<IHashRepository, HashRepository>();

        services.AddSingleton(sp =>
            new RabbitMqHashConsumer(
                sp.GetRequiredService<IConnectionFactory>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<RabbitMqHashConsumer>>(),
                queueName));

        return services;
    }
}

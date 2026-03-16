using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace HashProcessing.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMq(
        this IServiceCollection services,
        string hostName,
        string userName,
        string password,
        int publisherChannelPoolSize = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        });

        services.AddSingleton(sp =>
            sp.GetRequiredService<IConnectionFactory>().CreateConnectionAsync().GetAwaiter().GetResult());

        services.AddSingleton<PublisherChannelPool>(sp => new PublisherChannelPool(
            sp.GetRequiredService<IConnection>(),
            sp.GetRequiredService<ILogger<PublisherChannelPool>>(),
            publisherChannelPoolSize));

        services.AddSingleton<RabbitMqPublisher>();

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace HashProcessing.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMq(this IServiceCollection services, string hostName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);

        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = hostName,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        });

        services.AddSingleton(sp =>
            sp.GetRequiredService<IConnectionFactory>().CreateConnectionAsync().GetAwaiter().GetResult());

        services.AddSingleton<ConsumerChannelPool>();
        services.AddSingleton<PublisherChannelPool>();

        services.AddSingleton<RabbitMqPublisher>();

        return services;
    }
}

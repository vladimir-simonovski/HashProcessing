using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace HashProcessing.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMq(
        this IServiceCollection services,
        string hostName,
        string? userName = null,
        string? password = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);

        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName ?? ConnectionFactory.DefaultUser,
            Password = password ?? ConnectionFactory.DefaultPass,
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

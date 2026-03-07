namespace HashProcessing.Worker.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<ProcessReceivedHashesCommandHandler>();

        return services;
    }
}

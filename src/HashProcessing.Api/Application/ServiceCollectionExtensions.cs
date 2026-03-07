namespace HashProcessing.Api.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<GenerateHashesCommandHandler>();

        return services;
    }
}

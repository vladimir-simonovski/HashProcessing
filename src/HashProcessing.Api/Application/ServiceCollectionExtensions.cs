namespace HashProcessing.Api.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<GenerateHashesCommandHandler>();
        services.AddTransient<GetHashesQueryHandler>();
        services.AddTransient<UpsertHashDailyCountCommandHandler>();

        return services;
    }
}

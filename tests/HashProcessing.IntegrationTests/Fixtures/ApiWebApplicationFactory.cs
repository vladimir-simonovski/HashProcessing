using HashProcessing.Api;
using HashProcessing.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitMQ.Client;

namespace HashProcessing.IntegrationTests.Fixtures;

public class ApiWebApplicationFactory(string dbConnectionString, string rabbitMqConnectionString)
    : WebApplicationFactory<Program>
{
    private readonly string _dbConnectionString = dbConnectionString ?? throw new ArgumentNullException(nameof(dbConnectionString));
    private readonly string _rabbitMqConnectionString = rabbitMqConnectionString ?? throw new ArgumentNullException(nameof(rabbitMqConnectionString));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApiDbContext>>();
            services.AddDbContext<ApiDbContext>(options =>
                options.UseMySql(_dbConnectionString, new MariaDbServerVersion(new Version(11, 0))));

            services.RemoveAll<IConnectionFactory>();
            services.AddSingleton<IConnectionFactory>(new ConnectionFactory
            {
                Uri = new Uri(_rabbitMqConnectionString)
            });
        });
    }
}
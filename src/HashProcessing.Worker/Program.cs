using HashProcessing.Worker;
using HashProcessing.Worker.Application;
using HashProcessing.Worker.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<HashDbContext>("mariadb", tags: ["ready"])
    .AddRabbitMQ(tags: ["ready"]);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HashDbContext>();
    await db.Database.MigrateAsync();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();
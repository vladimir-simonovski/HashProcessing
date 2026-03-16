using HashProcessing.Worker;
using HashProcessing.Worker.Application;
using HashProcessing.Worker.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<HashDbContext>("mariadb");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HashDbContext>();
    await db.Database.MigrateAsync();
}

app.MapHealthChecks("/health");

app.Run();
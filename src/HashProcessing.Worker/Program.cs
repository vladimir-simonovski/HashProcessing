using HashProcessing.Worker;
using HashProcessing.Worker.Application;
using HashProcessing.Worker.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HashDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
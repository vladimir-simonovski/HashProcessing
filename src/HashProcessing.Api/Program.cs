using HashProcessing.Api.Application;
using HashProcessing.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/hashes", async (HttpContext context) =>
    {
        await context
            .RequestServices
            .GetRequiredService<GenerateHashesCommandHandler>()
            .HandleAsync(
                new GenerateHashesCommand(),
                context.RequestAborted);

        return Results.Accepted();
    })
    .WithName("PostHashes")
    .WithOpenApi();

app.MapGet("/hashes", async (HttpContext context) =>
    {
        var handler = context
            .RequestServices
            .GetRequiredService<GetHashesQueryHandler>();

        var result = await handler.HandleAsync(context.RequestAborted);
        return Results.Ok(result);
    })
    .WithName("GetHashes")
    .WithOpenApi();

app.Run();

namespace HashProcessing.Api
{
    public partial class Program { }   
}
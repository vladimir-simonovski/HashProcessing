using HashProcessing.Api.Application;
using HashProcessing.Api.Infrastructure;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "HashProcessing API",
        Version = "v1",
        Description = "API for generating SHA-1 hashes and retrieving daily hash count aggregations."
    });
});

builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("hash-generation", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<ApiDbContext>("mariadb", tags: ["ready"])
    .AddRabbitMQ(tags: ["ready"]);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapPost("/hashes", async (uint? count, HttpContext context) =>
    {
        await context
            .RequestServices
            .GetRequiredService<GenerateHashesCommandHandler>()
            .HandleAsync(
                new GenerateHashesCommand(count),
                context.RequestAborted);

        return Results.Accepted();
    })
    .WithName("PostHashes")
    .WithDescription("Generates SHA-1 hashes and publishes them to the processing pipeline via RabbitMQ. Defaults to 40,000 hashes if count is not specified.")
    .WithSummary("Generate SHA-1 hashes")
    .WithTags("Hashes")
    .Produces(StatusCodes.Status202Accepted)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status429TooManyRequests)
    .WithOpenApi(operation =>
    {
        operation.Parameters[0].Description = "Number of SHA-1 hashes to generate. Must be greater than zero. Defaults to 40,000.";
        return operation;
    })
    .RequireRateLimiting("hash-generation");

app.MapGet("/hashes", async (HttpContext context) =>
    {
        var handler = context
            .RequestServices
            .GetRequiredService<GetHashesQueryHandler>();

        var result = await handler.HandleAsync(context.RequestAborted);
        return Results.Ok(result);
    })
    .WithName("GetHashes")
    .WithDescription("Returns the aggregated daily hash counts, ordered by date descending.")
    .WithSummary("Get daily hash counts")
    .WithTags("Hashes")
    .Produces<HashesResponse>()
    .WithOpenApi();

app.Run();

namespace HashProcessing.Api
{
    // ReSharper disable once PartialTypeWithSinglePart
    [UsedImplicitly]
    public partial class Program { }   
}
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HashProcessing.Api.Infrastructure;

[UsedImplicitly]
public class ApiDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApiDbContext>
{
    public ApiDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApiDbContext>();
        optionsBuilder.UseMySql(
            "Server=localhost;Database=api;User=root;Password=root;",
            new MariaDbServerVersion(new Version(11, 0)));

        return new ApiDbContext(optionsBuilder.Options);
    }
}

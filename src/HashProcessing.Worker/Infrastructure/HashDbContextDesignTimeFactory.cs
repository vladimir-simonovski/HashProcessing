using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HashProcessing.Worker.Infrastructure;

public class HashDbContextDesignTimeFactory : IDesignTimeDbContextFactory<HashDbContext>
{
    public HashDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HashDbContext>();
        optionsBuilder.UseMySql(
            "Server=localhost;Database=worker;User=root;Password=root;",
            new MariaDbServerVersion(new Version(11, 0)));

        return new HashDbContext(optionsBuilder.Options);
    }
}

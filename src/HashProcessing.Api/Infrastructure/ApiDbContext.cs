using HashProcessing.Api.Core;
using Microsoft.EntityFrameworkCore;

namespace HashProcessing.Api.Infrastructure;

public class ApiDbContext(DbContextOptions<ApiDbContext> options) : DbContext(options)
{
    public DbSet<HashDailyCount> HashDailyCounts => Set<HashDailyCount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HashDailyCount>(entity =>
        {
            entity.ToTable("hash_daily_counts");

            entity.HasKey(e => e.Date);

            entity.Property(e => e.Date)
                .HasColumnName("date");

            entity.Property(e => e.Count)
                .HasColumnName("count")
                .IsRequired();
        });
    }
}

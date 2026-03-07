using HashProcessing.Worker.Core;
using Microsoft.EntityFrameworkCore;

namespace HashProcessing.Worker.Infrastructure;

public class HashDbContext(DbContextOptions<HashDbContext> options) : DbContext(options)
{
    public DbSet<HashEntity> Hashes => Set<HashEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HashEntity>(entity =>
        {
            entity.ToTable("hashes");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasMaxLength(36);

            entity.Property(e => e.Date)
                .HasColumnName("date")
                .IsRequired();

            entity.Property(e => e.Sha1)
                .HasColumnName("sha1")
                .HasMaxLength(40)
                .IsRequired();

            entity.HasIndex(e => e.Date);
        });
    }
}

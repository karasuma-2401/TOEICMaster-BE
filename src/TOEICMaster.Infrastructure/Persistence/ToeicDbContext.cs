using Microsoft.EntityFrameworkCore;

namespace TOEICMaster.Infrastructure.Persistence;

public sealed class ToeicDbContext(DbContextOptions<ToeicDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ToeicDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

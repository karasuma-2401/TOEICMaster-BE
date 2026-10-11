
using Microsoft.EntityFrameworkCore;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence;

public sealed class ToeicDbContext(
    DbContextOptions<ToeicDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ToeicDbContext).Assembly);
    }
}

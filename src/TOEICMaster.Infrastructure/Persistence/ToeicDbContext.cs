using Microsoft.EntityFrameworkCore;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence;

public sealed class ToeicDbContext(DbContextOptions<ToeicDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<EmailVerificationChallenge> EmailVerificationChallenges => Set<EmailVerificationChallenge>();
    public DbSet<PasswordResetChallenge> PasswordResetChallenges => Set<PasswordResetChallenge>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ToeicDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

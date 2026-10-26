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
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamRevision> ExamRevisions => Set<ExamRevision>();
    public DbSet<ExamBlock> ExamBlocks => Set<ExamBlock>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<ExamContextBlock> ExamContextBlocks => Set<ExamContextBlock>();
    public DbSet<ExamMediaAsset> ExamMediaAssets => Set<ExamMediaAsset>();
    public DbSet<ScoreProfile> ScoreProfiles => Set<ScoreProfile>();
    public DbSet<ScoreConversion> ScoreConversions => Set<ScoreConversion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ToeicDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

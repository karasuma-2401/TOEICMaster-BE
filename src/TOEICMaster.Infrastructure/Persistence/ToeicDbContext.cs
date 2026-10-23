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
    public DbSet<WritingExercise> WritingExercises => Set<WritingExercise>();
    public DbSet<WritingMediaAsset> WritingMediaAssets => Set<WritingMediaAsset>();
    public DbSet<WritingTest> WritingTests => Set<WritingTest>();
    public DbSet<WritingTestQuestion> WritingTestQuestions => Set<WritingTestQuestion>();
    public DbSet<WritingAttempt> WritingAttempts => Set<WritingAttempt>();
    public DbSet<WritingAttemptQuestion> WritingAttemptQuestions => Set<WritingAttemptQuestion>();
    public DbSet<WritingEvaluation> WritingEvaluations => Set<WritingEvaluation>();
    public DbSet<WritingEvaluationFeedback> WritingEvaluationFeedback => Set<WritingEvaluationFeedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ToeicDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

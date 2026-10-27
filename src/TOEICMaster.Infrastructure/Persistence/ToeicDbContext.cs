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
    public DbSet<SpeakingMediaAsset> SpeakingMediaAssets => Set<SpeakingMediaAsset>();
    public DbSet<SpeakingExercise> SpeakingExercises => Set<SpeakingExercise>();
    public DbSet<SpeakingTest> SpeakingTests => Set<SpeakingTest>();
    public DbSet<SpeakingTestExercise> SpeakingTestExercises => Set<SpeakingTestExercise>();
    public DbSet<SpeakingAttempt> SpeakingAttempts => Set<SpeakingAttempt>();
    public DbSet<SpeakingAttemptQuestion> SpeakingAttemptQuestions => Set<SpeakingAttemptQuestion>();
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

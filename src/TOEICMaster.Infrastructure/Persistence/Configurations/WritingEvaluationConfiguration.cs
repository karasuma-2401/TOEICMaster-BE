using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class WritingEvaluationConfiguration : IEntityTypeConfiguration<WritingEvaluation>
{
    public void Configure(EntityTypeBuilder<WritingEvaluation> builder)
    {
        builder.ToTable("writing_evaluations");
        builder.HasKey(evaluation => evaluation.Id);
        builder.Property(evaluation => evaluation.Provider).HasMaxLength(32).IsRequired();
        builder.Property(evaluation => evaluation.Model).HasMaxLength(100).IsRequired();
        builder.Property(evaluation => evaluation.RubricVersion).HasMaxLength(64).IsRequired();
        builder.Property(evaluation => evaluation.Status).HasConversion<short>().IsRequired();
        builder.Property(evaluation => evaluation.Summary).HasMaxLength(2_000);
        builder.Property(evaluation => evaluation.StrengthsJson).HasColumnType("jsonb");
        builder.Property(evaluation => evaluation.ImprovementsJson).HasColumnType("jsonb");
        builder.Property(evaluation => evaluation.FailureCode).HasMaxLength(80);
        builder.Property(evaluation => evaluation.FailureMessage).HasMaxLength(400);
        builder.Property(evaluation => evaluation.Version).IsConcurrencyToken();
        builder.HasIndex(evaluation => evaluation.WritingAttemptId).IsUnique();
        builder.HasIndex(evaluation => new { evaluation.Status, evaluation.NextAttemptAtUtc });
        builder.HasIndex(evaluation => evaluation.LeaseExpiresAtUtc);
        builder.HasOne<WritingAttempt>().WithOne().HasForeignKey<WritingEvaluation>(evaluation => evaluation.WritingAttemptId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(evaluation => evaluation.Feedback).WithOne().HasForeignKey(feedback => feedback.WritingEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WritingEvaluationFeedbackConfiguration : IEntityTypeConfiguration<WritingEvaluationFeedback>
{
    public void Configure(EntityTypeBuilder<WritingEvaluationFeedback> builder)
    {
        builder.ToTable("writing_evaluation_feedback");
        builder.HasKey(feedback => feedback.Id);
        builder.Property(feedback => feedback.Feedback).HasMaxLength(1_500).IsRequired();
        builder.Property(feedback => feedback.SuggestedRevision).HasMaxLength(1_500).IsRequired();
        builder.Property(feedback => feedback.CriterionScore).IsRequired();
        builder.HasIndex(feedback => new { feedback.WritingEvaluationId, feedback.QuestionPosition }).IsUnique();
    }
}

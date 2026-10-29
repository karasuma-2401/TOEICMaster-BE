using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class SpeakingEvaluationConfiguration : IEntityTypeConfiguration<SpeakingEvaluation>
{
    public void Configure(EntityTypeBuilder<SpeakingEvaluation> builder)
    {
        builder.ToTable("speaking_evaluations", table =>
        {
            table.HasCheckConstraint("ck_speaking_evaluations_status",
                "status IN ('QUEUED', 'PROCESSING', 'RETRYING', 'COMPLETED', 'FAILED')");
            table.HasCheckConstraint("ck_speaking_evaluations_estimated_score",
                "estimated_score IS NULL OR (estimated_score BETWEEN 0 AND 200 AND estimated_score % 10 = 0)");
            table.HasCheckConstraint("ck_speaking_evaluations_retry_count", "retry_count >= 0");
        });
        builder.HasKey(evaluation => evaluation.Id);
        builder.Property(evaluation => evaluation.Id).HasColumnName("id");
        builder.Property(evaluation => evaluation.AttemptId).HasColumnName("attempt_id").IsRequired();
        builder.Property(evaluation => evaluation.Status).HasColumnName("status").HasMaxLength(12)
            .HasConversion(SpeakingColumns.Code<SpeakingEvaluationStatus>()).IsRequired();
        builder.Property(evaluation => evaluation.SpeechProvider).HasColumnName("speech_provider").HasMaxLength(32)
            .IsRequired();
        builder.Property(evaluation => evaluation.SpeechLocale).HasColumnName("speech_locale").HasMaxLength(16)
            .IsRequired();
        builder.Property(evaluation => evaluation.LanguageProvider).HasColumnName("language_provider")
            .HasMaxLength(32).IsRequired();
        builder.Property(evaluation => evaluation.LanguageModel).HasColumnName("language_model").HasMaxLength(100)
            .IsRequired();
        builder.Property(evaluation => evaluation.RubricVersion).HasColumnName("rubric_version").HasMaxLength(64)
            .IsRequired();
        builder.Property(evaluation => evaluation.ConsentAcceptedAtUtc).HasColumnName("consent_accepted_at_utc")
            .IsRequired();
        builder.Property(evaluation => evaluation.QueuedAtUtc).HasColumnName("queued_at_utc");
        builder.Property(evaluation => evaluation.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(evaluation => evaluation.LeaseExpiresAtUtc).HasColumnName("lease_expires_at_utc");
        builder.Property(evaluation => evaluation.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(evaluation => evaluation.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(evaluation => evaluation.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(evaluation => evaluation.EstimatedScore).HasColumnName("estimated_score");
        builder.Property(evaluation => evaluation.Summary).HasColumnName("summary").HasMaxLength(2_000);
        builder.Property(evaluation => evaluation.StrengthsJson).HasColumnName("strengths_json").HasColumnType("jsonb");
        builder.Property(evaluation => evaluation.ImprovementsJson).HasColumnName("improvements_json")
            .HasColumnType("jsonb");
        builder.Property(evaluation => evaluation.FailureCode).HasColumnName("failure_code").HasMaxLength(80);
        builder.Property(evaluation => evaluation.FailureMessage).HasColumnName("failure_message").HasMaxLength(400);
        builder.Property(evaluation => evaluation.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(evaluation => evaluation.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Property(evaluation => evaluation.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.HasIndex(evaluation => evaluation.AttemptId).IsUnique();
        builder.HasIndex(evaluation => new { evaluation.Status, evaluation.NextAttemptAtUtc });
        builder.HasIndex(evaluation => evaluation.LeaseExpiresAtUtc);
        builder.HasOne<SpeakingAttempt>().WithOne().HasForeignKey<SpeakingEvaluation>(evaluation => evaluation.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(evaluation => evaluation.Items).WithOne().HasForeignKey(item => item.EvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(evaluation => evaluation.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class SpeakingEvaluationItemConfiguration : IEntityTypeConfiguration<SpeakingEvaluationItem>
{
    public void Configure(EntityTypeBuilder<SpeakingEvaluationItem> builder)
    {
        builder.ToTable("speaking_evaluation_items", table =>
        {
            table.HasCheckConstraint("ck_speaking_evaluation_items_part", "part BETWEEN 1 AND 5");
            table.HasCheckConstraint("ck_speaking_evaluation_items_recognition_status",
                "recognition_status IN ('RECOGNIZED', 'NO_SPEECH', 'NO_RECORDING')");
            table.HasCheckConstraint("ck_speaking_evaluation_items_raw_score",
                "raw_score IS NULL OR raw_score BETWEEN 0 AND max_raw_score");
            table.HasCheckConstraint("ck_speaking_evaluation_items_max_raw_score", "max_raw_score IN (3, 5)");
            table.HasCheckConstraint("ck_speaking_evaluation_items_language_scores",
                "(grammar_score IS NULL OR grammar_score BETWEEN 0 AND 100) AND " +
                "(vocabulary_score IS NULL OR vocabulary_score BETWEEN 0 AND 100) AND " +
                "(relevance_score IS NULL OR relevance_score BETWEEN 0 AND 100)");
        });
        builder.HasKey(item => new { item.EvaluationId, item.Position });
        builder.Property(item => item.EvaluationId).HasColumnName("evaluation_id");
        builder.Property(item => item.Position).HasColumnName("position").HasConversion<short>();
        builder.Property(item => item.Part).HasColumnName("part").HasConversion<short>().IsRequired();
        builder.Property(item => item.RecognitionStatus).HasColumnName("recognition_status").HasMaxLength(16)
            .HasConversion(SpeakingColumns.Code<SpeakingRecognitionStatus>()).IsRequired();
        builder.Property(item => item.Transcript).HasColumnName("transcript").HasMaxLength(5_000).IsRequired();
        builder.Property(item => item.AudioSeconds).HasColumnName("audio_seconds").IsRequired();
        builder.Property(item => item.AccuracyScore).HasColumnName("accuracy_score").IsRequired();
        builder.Property(item => item.FluencyScore).HasColumnName("fluency_score").IsRequired();
        builder.Property(item => item.CompletenessScore).HasColumnName("completeness_score");
        builder.Property(item => item.ProsodyScore).HasColumnName("prosody_score");
        builder.Property(item => item.PronunciationScore).HasColumnName("pronunciation_score").IsRequired();
        builder.Property(item => item.WordIssuesJson).HasColumnName("word_issues_json").HasColumnType("jsonb")
            .IsRequired();
        builder.Property(item => item.GrammarScore).HasColumnName("grammar_score").HasConversion<short?>();
        builder.Property(item => item.VocabularyScore).HasColumnName("vocabulary_score").HasConversion<short?>();
        builder.Property(item => item.RelevanceScore).HasColumnName("relevance_score").HasConversion<short?>();
        builder.Property(item => item.RawScore).HasColumnName("raw_score").HasConversion<short?>();
        builder.Property(item => item.MaxRawScore).HasColumnName("max_raw_score").HasConversion<short>().IsRequired();
        builder.Property(item => item.Feedback).HasColumnName("feedback").HasMaxLength(1_500);
        builder.Property(item => item.SuggestedResponse).HasColumnName("suggested_response").HasMaxLength(1_500);
    }
}

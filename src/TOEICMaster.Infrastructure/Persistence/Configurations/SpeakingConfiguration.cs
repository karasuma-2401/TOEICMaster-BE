using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

// Speaking tables use snake_case columns and upper-case varchar enums, as agreed in the frontend handoff.
internal static class SpeakingColumns
{
    public static ValueConverter<T, string> Code<T>() where T : struct, Enum =>
        new(value => CodeOf(value), value => SpeakingCodes.Parse<T>(value));

    private static string CodeOf<T>(T value) where T : struct, Enum => value switch
    {
        SpeakingDifficulty difficulty => difficulty.ToCode(),
        SpeakingExerciseStatus status => status.ToCode(),
        SpeakingTestStatus status => status.ToCode(),
        SpeakingMediaKind kind => kind.ToCode(),
        SpeakingAttemptMode mode => mode.ToCode(),
        SpeakingAttemptStatus status => status.ToCode(),
        SpeakingPhase phase => phase.ToCode(),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

public sealed class SpeakingMediaAssetConfiguration : IEntityTypeConfiguration<SpeakingMediaAsset>
{
    public void Configure(EntityTypeBuilder<SpeakingMediaAsset> builder)
    {
        builder.ToTable("speaking_media_assets", table =>
        {
            table.HasCheckConstraint("ck_speaking_media_assets_kind", "kind IN ('IMAGE', 'RECORDING')");
            table.HasCheckConstraint("ck_speaking_media_assets_size_bytes", "size_bytes > 0");
        });
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.Id).HasColumnName("id");
        builder.Property(asset => asset.Kind).HasColumnName("kind").HasMaxLength(16)
            .HasConversion(SpeakingColumns.Code<SpeakingMediaKind>()).IsRequired();
        builder.Property(asset => asset.ObjectKey).HasColumnName("object_key").HasMaxLength(256).IsRequired();
        builder.Property(asset => asset.ContentType).HasColumnName("content_type").HasMaxLength(64).IsRequired();
        builder.Property(asset => asset.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(asset => asset.Sha256).HasColumnName("sha256").HasColumnType("character(64)").IsRequired();
        builder.Property(asset => asset.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(asset => asset.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasIndex(asset => asset.ObjectKey).IsUnique();
    }
}

public sealed class SpeakingExerciseConfiguration : IEntityTypeConfiguration<SpeakingExercise>
{
    public void Configure(EntityTypeBuilder<SpeakingExercise> builder)
    {
        builder.ToTable("speaking_exercises", table =>
        {
            table.HasCheckConstraint("ck_speaking_exercises_part", "part BETWEEN 1 AND 5");
            table.HasCheckConstraint("ck_speaking_exercises_image", "(part = 2) = (image_asset_id IS NOT NULL)");
            table.HasCheckConstraint("ck_speaking_exercises_difficulty", "difficulty IN ('EASY', 'MEDIUM', 'HARD')");
            table.HasCheckConstraint("ck_speaking_exercises_status",
                "status IN ('DRAFT', 'PUBLISHED', 'ARCHIVED')");
        });
        builder.HasKey(exercise => exercise.Id);
        builder.Property(exercise => exercise.Id).HasColumnName("id");
        builder.Property(exercise => exercise.Part).HasColumnName("part").HasConversion<short>().IsRequired();
        builder.Property(exercise => exercise.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(exercise => exercise.Topic).HasColumnName("topic").HasMaxLength(100).IsRequired();
        builder.Property(exercise => exercise.Difficulty).HasColumnName("difficulty").HasMaxLength(8)
            .HasConversion(SpeakingColumns.Code<SpeakingDifficulty>()).IsRequired();
        builder.Property(exercise => exercise.Status).HasColumnName("status").HasMaxLength(12)
            .HasConversion(SpeakingColumns.Code<SpeakingExerciseStatus>()).IsRequired();
        builder.Property(exercise => exercise.ContentJson).HasColumnName("content_json").HasColumnType("jsonb")
            .IsRequired();
        builder.Property(exercise => exercise.ImageAssetId).HasColumnName("image_asset_id");
        builder.Property(exercise => exercise.RubricNotes).HasColumnName("rubric_notes").HasMaxLength(2_000)
            .HasDefaultValue(string.Empty).IsRequired();
        builder.Property(exercise => exercise.ModelAnswer).HasColumnName("model_answer").HasMaxLength(4_000)
            .IsRequired();
        builder.Property(exercise => exercise.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.Property(exercise => exercise.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(exercise => exercise.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
        builder.Property(exercise => exercise.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(exercise => exercise.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(exercise => new { exercise.Part, exercise.Status, exercise.UpdatedAtUtc, exercise.Id })
            .IsDescending(false, false, true, false)
            .HasDatabaseName("ix_speaking_exercises_part_status_updated_at_utc_id");
        builder.HasOne<SpeakingMediaAsset>().WithMany().HasForeignKey(exercise => exercise.ImageAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SpeakingTestConfiguration : IEntityTypeConfiguration<SpeakingTest>
{
    public void Configure(EntityTypeBuilder<SpeakingTest> builder)
    {
        builder.ToTable("speaking_tests", table =>
        {
            table.HasCheckConstraint("ck_speaking_tests_status", "status IN ('DRAFT', 'PUBLISHED')");
            table.HasCheckConstraint("ck_speaking_tests_published_at",
                "status <> 'PUBLISHED' OR published_at_utc IS NOT NULL");
        });
        builder.HasKey(test => test.Id);
        builder.Property(test => test.Id).HasColumnName("id");
        builder.Property(test => test.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(test => test.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(test => test.Description).HasColumnName("description").HasMaxLength(2_000)
            .HasDefaultValue(string.Empty).IsRequired();
        builder.Property(test => test.Status).HasColumnName("status").HasMaxLength(12)
            .HasConversion(SpeakingColumns.Code<SpeakingTestStatus>()).IsRequired();
        builder.Property(test => test.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.Property(test => test.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.Property(test => test.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(test => test.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
        builder.Property(test => test.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(test => test.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Ignore(test => test.IsDraft);
        builder.HasIndex(test => new { test.Status, test.UpdatedAtUtc, test.Id }).IsDescending(false, true, false)
            .HasDatabaseName("ix_speaking_tests_status_updated_at_utc_id");
    }
}

public sealed class SpeakingTestExerciseConfiguration : IEntityTypeConfiguration<SpeakingTestExercise>
{
    public void Configure(EntityTypeBuilder<SpeakingTestExercise> builder)
    {
        builder.ToTable("speaking_test_exercises", table =>
        {
            table.HasCheckConstraint("ck_speaking_test_exercises_part", "part BETWEEN 1 AND 5");
            table.HasCheckConstraint("ck_speaking_test_exercises_sequence_number", "sequence_number >= 1");
        });
        builder.HasKey(slot => new { slot.TestId, slot.Part, slot.SequenceNumber });
        builder.Property(slot => slot.TestId).HasColumnName("test_id");
        builder.Property(slot => slot.Part).HasColumnName("part").HasConversion<short>();
        builder.Property(slot => slot.SequenceNumber).HasColumnName("sequence_number").HasConversion<short>();
        builder.Property(slot => slot.ExerciseId).HasColumnName("exercise_id").IsRequired();
        builder.Property(slot => slot.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb");
        builder.HasIndex(slot => new { slot.TestId, slot.ExerciseId }).IsUnique();
        builder.HasIndex(slot => slot.ExerciseId);
        builder.HasOne<SpeakingTest>().WithMany().HasForeignKey(slot => slot.TestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SpeakingExercise>().WithMany().HasForeignKey(slot => slot.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SpeakingAttemptConfiguration : IEntityTypeConfiguration<SpeakingAttempt>
{
    public void Configure(EntityTypeBuilder<SpeakingAttempt> builder)
    {
        builder.ToTable("speaking_attempts", table =>
        {
            table.HasCheckConstraint("ck_speaking_attempts_owner",
                "(owner_user_id IS NOT NULL) <> (anonymous_session_hash IS NOT NULL)");
            table.HasCheckConstraint("ck_speaking_attempts_mode",
                "mode IN ('FULL', 'PART_1', 'PART_2', 'PART_3', 'PART_4', 'PART_5')");
            table.HasCheckConstraint("ck_speaking_attempts_status", "status IN ('IN_PROGRESS', 'SUBMITTED')");
            table.HasCheckConstraint("ck_speaking_attempts_current_phase",
                "current_phase IS NULL OR current_phase IN ('READING', 'PREPARING', 'RESPONDING')");
        });
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).HasColumnName("id");
        builder.Property(attempt => attempt.TestId).HasColumnName("test_id").IsRequired();
        builder.Property(attempt => attempt.Mode).HasColumnName("mode").HasMaxLength(8)
            .HasConversion(SpeakingColumns.Code<SpeakingAttemptMode>()).IsRequired();
        builder.Property(attempt => attempt.Status).HasColumnName("status").HasMaxLength(12)
            .HasConversion(SpeakingColumns.Code<SpeakingAttemptStatus>()).IsRequired();
        builder.Property(attempt => attempt.OwnerUserId).HasColumnName("owner_user_id");
        builder.Property(attempt => attempt.AnonymousSessionHash).HasColumnName("anonymous_session_hash")
            .HasColumnType("character(64)");
        builder.Property(attempt => attempt.CurrentPosition).HasColumnName("current_position")
            .HasConversion<short?>();
        builder.Property(attempt => attempt.CurrentPhase).HasColumnName("current_phase").HasMaxLength(12)
            .HasConversion(new ValueConverter<SpeakingPhase?, string?>(
                value => value.HasValue ? value.Value.ToCode() : null,
                value => value == null ? null : SpeakingCodes.Parse<SpeakingPhase>(value)));
        builder.Property(attempt => attempt.PhaseStartedAtUtc).HasColumnName("phase_started_at_utc");
        builder.Property(attempt => attempt.PhaseEndsAtUtc).HasColumnName("phase_ends_at_utc");
        builder.Property(attempt => attempt.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(attempt => attempt.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Property(attempt => attempt.SubmittedAtUtc).HasColumnName("submitted_at_utc");
        builder.Property(attempt => attempt.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.HasIndex(attempt => new { attempt.OwnerUserId, attempt.CreatedAtUtc }).IsDescending(false, true)
            .HasDatabaseName("ix_speaking_attempts_owner_user_id_created_at_utc");
        builder.HasIndex(attempt => new { attempt.AnonymousSessionHash, attempt.CreatedAtUtc })
            .IsDescending(false, true).HasDatabaseName("ix_speaking_attempts_anonymous_session_hash_created_at_utc");
        builder.HasIndex(attempt => attempt.TestId);
        builder.HasOne<SpeakingTest>().WithMany().HasForeignKey(attempt => attempt.TestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(attempt => attempt.Questions).WithOne().HasForeignKey(question => question.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(attempt => attempt.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class SpeakingAttemptQuestionConfiguration : IEntityTypeConfiguration<SpeakingAttemptQuestion>
{
    public void Configure(EntityTypeBuilder<SpeakingAttemptQuestion> builder)
    {
        builder.ToTable("speaking_attempt_questions", table =>
        {
            table.HasCheckConstraint("ck_speaking_attempt_questions_part", "part BETWEEN 1 AND 5");
            table.HasCheckConstraint("ck_speaking_attempt_questions_question_index", "question_index BETWEEN 0 AND 2");
        });
        builder.HasKey(question => new { question.AttemptId, question.Position });
        builder.Property(question => question.AttemptId).HasColumnName("attempt_id");
        builder.Property(question => question.Position).HasColumnName("position").HasConversion<short>();
        builder.Property(question => question.Part).HasColumnName("part").HasConversion<short>().IsRequired();
        builder.Property(question => question.QuestionIndex).HasColumnName("question_index").HasConversion<short>()
            .IsRequired();
        builder.Property(question => question.ExerciseId).HasColumnName("exercise_id").IsRequired();
        builder.Property(question => question.ContentJson).HasColumnName("content_json").HasColumnType("jsonb")
            .IsRequired();
        builder.Property(question => question.ReviewJson).HasColumnName("review_json").HasColumnType("jsonb")
            .IsRequired();
        builder.Property(question => question.ReadingSeconds).HasColumnName("reading_seconds").HasConversion<short>();
        builder.Property(question => question.PreparationSeconds).HasColumnName("preparation_seconds")
            .HasConversion<short>();
        builder.Property(question => question.ResponseSeconds).HasColumnName("response_seconds")
            .HasConversion<short>();
        builder.Property(question => question.LockedAtUtc).HasColumnName("locked_at_utc");
        builder.Property(question => question.RecordingAssetId).HasColumnName("recording_asset_id");
        builder.Property(question => question.RecordingDurationSeconds).HasColumnName("recording_duration_seconds")
            .HasConversion<short?>();
        builder.Property(question => question.RecordingUploadedAtUtc).HasColumnName("recording_uploaded_at_utc");
        builder.HasIndex(question => question.RecordingAssetId);
        builder.HasOne<SpeakingMediaAsset>().WithMany().HasForeignKey(question => question.RecordingAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class WritingAttemptConfiguration : IEntityTypeConfiguration<WritingAttempt>
{
    public void Configure(EntityTypeBuilder<WritingAttempt> builder)
    {
        builder.ToTable("writing_attempts", table => table.HasCheckConstraint("CK_writing_attempt_owner",
            "(\"UserId\" IS NOT NULL AND \"AnonymousSessionHash\" IS NULL) OR (\"UserId\" IS NULL AND \"AnonymousSessionHash\" IS NOT NULL)"));
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.TestCode).HasMaxLength(64).IsRequired();
        builder.Property(attempt => attempt.TestTitle).HasMaxLength(200).IsRequired();
        builder.Property(attempt => attempt.Mode).HasConversion<short>().IsRequired();
        builder.Property(attempt => attempt.AnonymousSessionHash).HasMaxLength(64);
        builder.Property(attempt => attempt.Status).HasConversion<short>().IsRequired();
        builder.Property(attempt => attempt.CurrentPhase).HasConversion<short>().IsRequired();
        builder.HasIndex(attempt => new { attempt.UserId, attempt.UpdatedAtUtc });
        builder.HasIndex(attempt => new { attempt.AnonymousSessionHash, attempt.UpdatedAtUtc });
        builder.HasIndex(attempt => new { attempt.WritingTestId, attempt.CreatedAtUtc });
        builder.HasMany(attempt => attempt.Questions).WithOne().HasForeignKey(question => question.WritingAttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WritingAttemptQuestionConfiguration : IEntityTypeConfiguration<WritingAttemptQuestion>
{
    public void Configure(EntityTypeBuilder<WritingAttemptQuestion> builder)
    {
        builder.ToTable("writing_attempt_questions");
        builder.HasKey(question => question.Id);
        builder.Property(question => question.Part).HasConversion<short>().IsRequired();
        builder.Property(question => question.PromptSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(question => question.Answer).HasMaxLength(12_000).IsRequired();
        builder.HasIndex(question => new { question.WritingAttemptId, question.Position }).IsUnique();
        builder.HasIndex(question => question.WritingTestQuestionId);
    }
}

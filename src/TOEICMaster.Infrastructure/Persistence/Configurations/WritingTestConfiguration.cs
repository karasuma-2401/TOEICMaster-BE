using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class WritingTestConfiguration : IEntityTypeConfiguration<WritingTest>
{
    public void Configure(EntityTypeBuilder<WritingTest> builder)
    {
        builder.ToTable("writing_tests");
        builder.HasKey(test => test.Id);
        builder.Property(test => test.Code).HasMaxLength(64).IsRequired();
        builder.Property(test => test.Title).HasMaxLength(200).IsRequired();
        builder.Property(test => test.Description).HasMaxLength(2_000).IsRequired();
        builder.Property(test => test.Status).HasConversion<short>().IsRequired();
        builder.Property(test => test.Version).IsConcurrencyToken();
        builder.HasIndex(test => test.Code).IsUnique();
        builder.HasIndex(test => new { test.Status, test.UpdatedAtUtc });
        builder.HasMany(test => test.Questions).WithOne().HasForeignKey(question => question.WritingTestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WritingTestQuestionConfiguration : IEntityTypeConfiguration<WritingTestQuestion>
{
    public void Configure(EntityTypeBuilder<WritingTestQuestion> builder)
    {
        builder.ToTable("writing_test_questions");
        builder.HasKey(question => question.Id);
        builder.Property(question => question.Part).HasConversion<short>().IsRequired();
        builder.Property(question => question.SequenceNumber).IsRequired();
        builder.Property(question => question.SnapshotJson).HasColumnType("jsonb");
        builder.HasIndex(question => new { question.WritingTestId, question.Part, question.SequenceNumber }).IsUnique();
        builder.HasIndex(question => question.ExerciseId);
        builder.HasOne<WritingExercise>().WithMany().HasForeignKey(question => question.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

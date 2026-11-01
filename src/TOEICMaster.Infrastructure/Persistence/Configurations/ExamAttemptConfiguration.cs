using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class ExamAttemptConfiguration : IEntityTypeConfiguration<ExamAttempt>
{
    public void Configure(EntityTypeBuilder<ExamAttempt> builder)
    {
        builder.ToTable("ExamAttempts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.StartedAtUtc)
            .IsRequired();

        builder.Property(x => x.TimeLimitSeconds)
            .HasDefaultValue(7200)
            .IsRequired();

        builder.Property(x => x.IsTimeExpired)
            .HasDefaultValue(false);

        builder.HasMany(x => x.Answers)
            .WithOne()
            .HasForeignKey(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.RevisionId });
        builder.HasIndex(x => x.StartedAtUtc);
    }
}

public sealed class ExamAttemptAnswerConfiguration : IEntityTypeConfiguration<ExamAttemptAnswer>
{
    public void Configure(EntityTypeBuilder<ExamAttemptAnswer> builder)
    {
        builder.ToTable("ExamAttemptAnswers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SelectedKey)
            .HasMaxLength(8);

        builder.Property(x => x.CorrectKey)
            .HasMaxLength(8);

        builder.Property(x => x.IsFlagged)
            .HasDefaultValue(false);

        builder.HasIndex(x => new { x.AttemptId, x.QuestionId })
            .IsUnique();
    }
}

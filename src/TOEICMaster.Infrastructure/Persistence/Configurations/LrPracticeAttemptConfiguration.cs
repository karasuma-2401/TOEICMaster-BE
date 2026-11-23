using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class LrPracticeAttemptConfiguration
    : IEntityTypeConfiguration<LrPracticeAttempt>
{
    public void Configure(EntityTypeBuilder<LrPracticeAttempt> builder)
    {
        builder.ToTable("lr_practice_attempts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.SubmittedAtUtc)
            .IsRequired();

        builder.Property(x => x.TotalQuestions)
            .IsRequired();

        builder.Property(x => x.CorrectCount)
            .IsRequired();

        builder.Property(x => x.IncorrectCount)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prevent deleting an exercise if it already has practice attempt history.
        builder.HasOne<LrExerciseBankItem>()
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Answers)
            .WithOne()
            .HasForeignKey(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.ItemId });
        builder.HasIndex(x => x.SubmittedAtUtc);
    }
}

public sealed class LrPracticeAttemptAnswerConfiguration
    : IEntityTypeConfiguration<LrPracticeAttemptAnswer>
{
    public void Configure(EntityTypeBuilder<LrPracticeAttemptAnswer> builder)
    {
        builder.ToTable("lr_practice_attempt_answers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.SelectedKey)
            .HasMaxLength(8);

        builder.Property(x => x.CorrectKey)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(x => x.Translation);

        builder.Property(x => x.Explanation);

        builder.HasIndex(x => new { x.AttemptId, x.QuestionId })
            .IsUnique();
    }
}

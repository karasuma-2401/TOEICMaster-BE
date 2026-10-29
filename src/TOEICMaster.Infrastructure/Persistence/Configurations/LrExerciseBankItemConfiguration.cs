using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class LrExerciseBankItemConfiguration : IEntityTypeConfiguration<LrExerciseBankItem>
{
    public void Configure(EntityTypeBuilder<LrExerciseBankItem> builder)
    {
        builder.ToTable("lr_exercise_bank_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Part).HasConversion<short>().IsRequired();
        builder.Property(item => item.Kind).HasConversion<short>().IsRequired();
        builder.Property(item => item.Code).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Topic).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Difficulty).HasConversion<short>().IsRequired();
        builder.Property(item => item.Status).HasConversion<short>().IsRequired();
        builder.Property(item => item.Context).HasMaxLength(12_000);
        builder.Property(item => item.AudioUrl).HasMaxLength(2_000);
        builder.Property(item => item.ImageUrl).HasMaxLength(2_000);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasIndex(item => new { item.Part, item.Status, item.UpdatedAtUtc });
        builder.HasMany(item => item.Questions).WithOne().HasForeignKey(question => question.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LrExerciseBankQuestionConfiguration : IEntityTypeConfiguration<LrExerciseBankQuestion>
{
    public void Configure(EntityTypeBuilder<LrExerciseBankQuestion> builder)
    {
        builder.ToTable("lr_exercise_bank_questions");
        builder.HasKey(question => question.Id);
        builder.Property(question => question.Code).HasMaxLength(64).IsRequired();
        builder.Property(question => question.Prompt).HasMaxLength(4_000).IsRequired();
        builder.Property(question => question.OptionA).HasMaxLength(1_000).IsRequired();
        builder.Property(question => question.OptionB).HasMaxLength(1_000).IsRequired();
        builder.Property(question => question.OptionC).HasMaxLength(1_000).IsRequired();
        builder.Property(question => question.OptionD).HasMaxLength(1_000);
        builder.Property(question => question.CorrectKey).HasMaxLength(1).IsRequired();
        builder.Property(question => question.Translation).HasMaxLength(4_000);
        builder.Property(question => question.Explanation).HasMaxLength(4_000);
        builder.HasIndex(question => new { question.ItemId, question.Ordinal }).IsUnique();
        builder.HasIndex(question => question.Code).IsUnique();
    }
}

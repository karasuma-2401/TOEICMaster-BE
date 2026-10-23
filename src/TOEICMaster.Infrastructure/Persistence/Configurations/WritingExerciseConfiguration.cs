using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class WritingExerciseConfiguration : IEntityTypeConfiguration<WritingExercise>
{
    public void Configure(EntityTypeBuilder<WritingExercise> builder)
    {
        builder.ToTable("writing_exercises");
        builder.HasKey(exercise => exercise.Id);
        builder.Property(exercise => exercise.Part).HasConversion<short>().IsRequired();
        builder.Property(exercise => exercise.Code).HasMaxLength(64).IsRequired();
        builder.Property(exercise => exercise.Topic).HasMaxLength(100).IsRequired();
        builder.Property(exercise => exercise.Difficulty).HasConversion<short>().IsRequired();
        builder.Property(exercise => exercise.Status).HasConversion<short>().IsRequired();
        builder.Property(exercise => exercise.RubricNotes).HasMaxLength(4_000).IsRequired();
        builder.Property(exercise => exercise.ModelAnswer).HasMaxLength(12_000).IsRequired();
        builder.Property(exercise => exercise.ImageAlt).HasMaxLength(300);
        builder.Property(exercise => exercise.KeywordOne).HasMaxLength(100);
        builder.Property(exercise => exercise.KeywordTwo).HasMaxLength(100);
        builder.Property(exercise => exercise.Sender).HasMaxLength(200);
        builder.Property(exercise => exercise.Recipient).HasMaxLength(200);
        builder.Property(exercise => exercise.Subject).HasMaxLength(300);
        builder.Property(exercise => exercise.EmailBody).HasMaxLength(12_000);
        builder.Property(exercise => exercise.Directions).HasMaxLength(4_000);
        builder.Property(exercise => exercise.Prompt).HasMaxLength(4_000);
        builder.Property(exercise => exercise.Outline).HasMaxLength(8_000);
        builder.Property(exercise => exercise.Version).IsConcurrencyToken();
        builder.HasIndex(exercise => exercise.Code).IsUnique();
        builder.HasIndex(exercise => new { exercise.Part, exercise.Status, exercise.UpdatedAtUtc });
        builder.HasOne<WritingMediaAsset>().WithMany().HasForeignKey(exercise => exercise.ImageAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class ScoreProfileConfiguration : IEntityTypeConfiguration<ScoreProfile>
{
    public void Configure(EntityTypeBuilder<ScoreProfile> builder)
    {
        builder.ToTable("score_profiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Name).HasMaxLength(150).IsRequired();
        builder.Property(profile => profile.CreatedAtUtc).IsRequired();
        builder.HasIndex(profile => profile.Name).IsUnique();
        builder.HasMany(profile => profile.Conversions).WithOne().HasForeignKey(conversion => conversion.ScoreProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(profile => profile.Conversions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ScoreConversionConfiguration : IEntityTypeConfiguration<ScoreConversion>
{
    public void Configure(EntityTypeBuilder<ScoreConversion> builder)
    {
        builder.ToTable("score_conversions");
        builder.HasKey(conversion => new { conversion.ScoreProfileId, conversion.Skill, conversion.RawCorrectCount });
        builder.Property(conversion => conversion.Skill).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(conversion => conversion.RawCorrectCount).IsRequired();
        builder.Property(conversion => conversion.ScaledScore).IsRequired();
    }
}

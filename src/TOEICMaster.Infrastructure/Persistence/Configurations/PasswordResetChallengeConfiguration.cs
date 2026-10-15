using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetChallengeConfiguration : IEntityTypeConfiguration<PasswordResetChallenge>
{
    public void Configure(EntityTypeBuilder<PasswordResetChallenge> builder)
    {
        builder.ToTable("password_reset_challenges");
        builder.HasKey(challenge => challenge.UserId);
        builder.Property(challenge => challenge.CodeHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(challenge => challenge.SentAtUtc).IsRequired();
        builder.Property(challenge => challenge.ExpiresAtUtc).IsRequired();
        builder.Property(challenge => challenge.FailedAttempts).IsRequired();
        builder.Property(challenge => challenge.ResetTokenHash).HasMaxLength(64).IsFixedLength();
        builder.Property(challenge => challenge.ResetTokenExpiresAtUtc);
        builder.HasIndex(challenge => challenge.ResetTokenHash).IsUnique();
        builder.HasOne<User>().WithOne().HasForeignKey<PasswordResetChallenge>(challenge => challenge.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

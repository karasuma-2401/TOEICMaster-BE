using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class EmailVerificationChallengeConfiguration : IEntityTypeConfiguration<EmailVerificationChallenge>
{
    public void Configure(EntityTypeBuilder<EmailVerificationChallenge> builder)
    {
        builder.ToTable("email_verification_challenges");
        builder.HasKey(challenge => challenge.UserId);
        builder.Property(challenge => challenge.CodeHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(challenge => challenge.SentAtUtc).IsRequired();
        builder.Property(challenge => challenge.ExpiresAtUtc).IsRequired();
        builder.Property(challenge => challenge.FailedAttempts).IsRequired();
        builder.HasOne<User>().WithOne().HasForeignKey<EmailVerificationChallenge>(challenge => challenge.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

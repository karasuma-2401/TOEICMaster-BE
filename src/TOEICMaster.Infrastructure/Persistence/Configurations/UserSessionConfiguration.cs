using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.RefreshTokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(session => session.CreatedAtUtc).IsRequired();
        builder.Property(session => session.ExpiresAtUtc).IsRequired();
        builder.HasIndex(session => session.RefreshTokenHash).IsUnique();
        builder.HasIndex(session => new { session.UserId, session.ExpiresAtUtc });
        builder.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserSession>().WithOne().HasForeignKey<UserSession>(session => session.ReplacedBySessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

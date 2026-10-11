using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class ExternalIdentityConfiguration : IEntityTypeConfiguration<ExternalIdentity>
{
    public void Configure(EntityTypeBuilder<ExternalIdentity> builder)
    {
        builder.ToTable("external_identities");
        builder.HasKey(identity => identity.Id);
        builder.Property(identity => identity.Provider).HasMaxLength(32).IsRequired();
        builder.Property(identity => identity.Subject).HasMaxLength(255).IsRequired();
        builder.Property(identity => identity.CreatedAtUtc).IsRequired();
        builder.HasIndex(identity => new { identity.Provider, identity.Subject }).IsUnique();
        builder.HasIndex(identity => identity.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(identity => identity.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

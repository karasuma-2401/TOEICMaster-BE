
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    private static readonly Guid AdminRoleId =
        Guid.Parse("a7f4a8d2-6e31-4c90-8b12-123456789001");

    private static readonly Guid UserRoleId =
        Guid.Parse("b8e5b9c3-7f42-4da1-9c23-123456789002");

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.Property(x => x.Description)
            .HasMaxLength(250);

        builder.HasData(
            new Role
            {
                Id = AdminRoleId,
                Name = "Admin",
                Description = "System administrator"
            },
            new Role
            {
                Id = UserRoleId,
                Name = "User",
                Description = "Regular user"
            });
    }
}

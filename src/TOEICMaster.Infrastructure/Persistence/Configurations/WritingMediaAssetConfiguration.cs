using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class WritingMediaAssetConfiguration : IEntityTypeConfiguration<WritingMediaAsset>
{
    public void Configure(EntityTypeBuilder<WritingMediaAsset> builder)
    {
        builder.ToTable("writing_media_assets");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.ObjectKey).HasMaxLength(256).IsRequired();
        builder.Property(asset => asset.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(asset => asset.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(asset => asset.ByteLength).IsRequired();
        builder.HasIndex(asset => asset.ObjectKey).IsUnique();
    }
}

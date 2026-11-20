using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeCategoryConfiguration : IEntityTypeConfiguration<KnowledgeCategory>
{
    public void Configure(EntityTypeBuilder<KnowledgeCategory> builder)
    {
        builder.ToTable("knowledge_categories", table =>
        {
            table.HasCheckConstraint("ck_knowledge_categories_slug_lower", "slug = lower(slug)");
            table.HasCheckConstraint("ck_knowledge_categories_display_order", "display_order >= 0");
        });
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).HasColumnName("id");
        builder.Property(category => category.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(category => category.Slug).HasColumnName("slug").HasMaxLength(120).IsRequired();
        builder.Property(category => category.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(category => category.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0)
            .IsRequired();
        builder.Property(category => category.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(category => category.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
        builder.Property(category => category.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(category => category.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Property(category => category.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();

        builder.HasIndex(category => category.Slug).IsUnique().HasDatabaseName("ux_knowledge_categories_slug");
        builder.HasIndex(category => new { category.DisplayOrder, category.Name })
            .HasDatabaseName("ix_knowledge_categories_display_order_name");

        builder.HasOne<User>().WithMany().HasForeignKey(category => category.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(category => category.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class KnowledgeTagConfiguration : IEntityTypeConfiguration<KnowledgeTag>
{
    public void Configure(EntityTypeBuilder<KnowledgeTag> builder)
    {
        builder.ToTable("knowledge_tags", table =>
        {
            table.HasCheckConstraint("ck_knowledge_tags_slug_lower", "slug = lower(slug)");
        });
        builder.HasKey(tag => tag.Id);
        builder.Property(tag => tag.Id).HasColumnName("id");
        builder.Property(tag => tag.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(tag => tag.Slug).HasColumnName("slug").HasMaxLength(64).IsRequired();
        builder.Property(tag => tag.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(tag => tag.Slug).IsUnique().HasDatabaseName("ux_knowledge_tags_slug");
    }
}

public sealed class KnowledgeMediaAssetConfiguration : IEntityTypeConfiguration<KnowledgeMediaAsset>
{
    public void Configure(EntityTypeBuilder<KnowledgeMediaAsset> builder)
    {
        builder.ToTable("knowledge_media_assets", table =>
        {
            table.HasCheckConstraint("ck_knowledge_media_assets_byte_length", "byte_length > 0 AND byte_length <= 5242880");
        });
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.Id).HasColumnName("id");
        builder.Property(asset => asset.ObjectKey).HasColumnName("object_key").HasMaxLength(256).IsRequired();
        builder.Property(asset => asset.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255)
            .IsRequired();
        builder.Property(asset => asset.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(asset => asset.ByteLength).HasColumnName("byte_length").IsRequired();
        builder.Property(asset => asset.Sha256).HasColumnName("sha256").HasColumnType("character(64)").IsRequired();
        builder.Property(asset => asset.UploadedByUserId).HasColumnName("uploaded_by_user_id").IsRequired();
        builder.Property(asset => asset.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(asset => asset.ObjectKey).IsUnique().HasDatabaseName("ux_knowledge_media_assets_object_key");
        builder.HasOne<User>().WithMany().HasForeignKey(asset => asset.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.ToTable("knowledge_articles", table =>
        {
            table.HasCheckConstraint("ck_knowledge_articles_slug_lower", "slug = lower(slug)");
            table.HasCheckConstraint("ck_knowledge_articles_skill", "skill BETWEEN 0 AND 4");
            table.HasCheckConstraint("ck_knowledge_articles_status", "status BETWEEN 1 AND 3");
            table.HasCheckConstraint("ck_knowledge_articles_read_minutes", "estimated_read_minutes >= 1");
            table.HasCheckConstraint("ck_knowledge_articles_published_at",
                "status <> 2 OR published_at_utc IS NOT NULL");
        });
        builder.HasKey(article => article.Id);
        builder.Property(article => article.Id).HasColumnName("id");
        builder.Property(article => article.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(article => article.Slug).HasColumnName("slug").HasMaxLength(220).IsRequired();
        builder.Property(article => article.Summary).HasColumnName("summary").HasMaxLength(500).IsRequired();
        builder.Property(article => article.Skill).HasColumnName("skill").HasConversion<short>().IsRequired();
        builder.Property(article => article.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(article => article.CoverImageAssetId).HasColumnName("cover_image_asset_id");
        builder.Property(article => article.CoverImageAlt).HasColumnName("cover_image_alt").HasMaxLength(300);
        builder.Property(article => article.ContentJson).HasColumnName("content_json").HasColumnType("jsonb")
            .IsRequired();
        builder.Property(article => article.TocJson).HasColumnName("toc_json").HasColumnType("jsonb").IsRequired();
        builder.Property(article => article.EstimatedReadMinutes).HasColumnName("estimated_read_minutes").IsRequired();
        builder.Property(article => article.Status).HasColumnName("status").HasConversion<short>().IsRequired();
        builder.Property(article => article.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.Property(article => article.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(article => article.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
        builder.Property(article => article.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(article => article.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Property(article => article.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.Ignore(article => article.CanBeDeleted);

        builder.HasIndex(article => article.Slug).IsUnique().HasDatabaseName("ux_knowledge_articles_slug");
        builder.HasIndex(article => new { article.Status, article.Skill, article.CategoryId, article.PublishedAtUtc })
            .IsDescending(false, false, false, true)
            .HasDatabaseName("ix_knowledge_articles_status_skill_category_published");
        builder.HasIndex(article => new { article.Status, article.UpdatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("ix_knowledge_articles_status_updated");
        builder.HasIndex(article => article.CategoryId).HasDatabaseName("ix_knowledge_articles_category_id");

        builder.HasOne<KnowledgeCategory>().WithMany().HasForeignKey(article => article.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeMediaAsset>().WithMany().HasForeignKey(article => article.CoverImageAssetId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(article => article.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(article => article.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(article => article.ArticleTags).WithOne().HasForeignKey(link => link.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(article => article.ArticleTags).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(article => article.MediaAssets).WithOne().HasForeignKey(link => link.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(article => article.MediaAssets).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class KnowledgeArticleTagConfiguration : IEntityTypeConfiguration<KnowledgeArticleTag>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleTag> builder)
    {
        builder.ToTable("knowledge_article_tags");
        builder.HasKey(link => new { link.ArticleId, link.TagId });
        builder.Property(link => link.ArticleId).HasColumnName("article_id");
        builder.Property(link => link.TagId).HasColumnName("tag_id");

        builder.HasIndex(link => link.TagId).HasDatabaseName("ix_knowledge_article_tags_tag_id");
        builder.HasOne<KnowledgeTag>().WithMany().HasForeignKey(link => link.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class KnowledgeArticleMediaAssetConfiguration : IEntityTypeConfiguration<KnowledgeArticleMediaAsset>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleMediaAsset> builder)
    {
        builder.ToTable("knowledge_article_media_assets");
        builder.HasKey(link => new { link.ArticleId, link.MediaAssetId });
        builder.Property(link => link.ArticleId).HasColumnName("article_id");
        builder.Property(link => link.MediaAssetId).HasColumnName("media_asset_id");

        builder.HasIndex(link => link.MediaAssetId).HasDatabaseName("ix_knowledge_article_media_assets_media_asset_id");
        builder.HasOne<KnowledgeMediaAsset>().WithMany().HasForeignKey(link => link.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

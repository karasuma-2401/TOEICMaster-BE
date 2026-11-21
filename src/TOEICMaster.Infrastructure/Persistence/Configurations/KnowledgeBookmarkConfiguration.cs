using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeBookmarkConfiguration : IEntityTypeConfiguration<KnowledgeBookmark>
{
    public void Configure(EntityTypeBuilder<KnowledgeBookmark> builder)
    {
        builder.ToTable("knowledge_bookmarks");
        builder.HasKey(b => new { b.UserId, b.ArticleId });
        builder.Property(b => b.UserId).HasColumnName("user_id");
        builder.Property(b => b.ArticleId).HasColumnName("article_id");
        builder.Property(b => b.SavedAtUtc).HasColumnName("saved_at_utc");
        builder.HasOne<User>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<KnowledgeArticle>().WithMany().HasForeignKey(b => b.ArticleId).OnDelete(DeleteBehavior.Cascade);
    }
}

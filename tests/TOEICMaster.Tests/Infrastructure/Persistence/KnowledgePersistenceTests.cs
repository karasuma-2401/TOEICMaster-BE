using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using TOEICMaster.Application.Knowledge.Admin;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.Knowledge;
using TOEICMaster.Infrastructure.Persistence.Migrations;

namespace TOEICMaster.Tests.Infrastructure.Persistence;

public sealed class KnowledgePersistenceTests
{
    private static ToeicDbContext CreateContext() => new(new DbContextOptionsBuilder<ToeicDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=test;Username=test;Password=test").Options);

    [Fact]
    public void Knowledge_migration_matches_current_model_and_generates_valid_postgresql_schema()
    {
        using var context = CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());

        var script = context.GetService<IMigrator>().GenerateScript(
            "20261118173947_RefactorLrAudioSchema", "20261120021557_AddKnowledgeBase");

        Assert.Contains("CREATE TABLE knowledge_categories", script);
        Assert.Contains("CREATE TABLE knowledge_articles", script);
        Assert.Contains("CREATE TABLE knowledge_tags", script);
        Assert.Contains("CREATE TABLE knowledge_article_tags", script);
        Assert.Contains("CREATE TABLE knowledge_media_assets", script);
        Assert.Contains("CREATE TABLE knowledge_article_media_assets", script);
    }

    [Fact]
    public void Knowledge_category_foreign_key_restricts_deletion_when_articles_exist()
    {
        var operations = new AddKnowledgeBaseProbe().GetUpOperations();
        var articlesTable = Assert.Single(operations.OfType<CreateTableOperation>(),
            operation => operation.Name == "knowledge_articles");
        var categoryFk = Assert.Single(articlesTable.ForeignKeys,
            fk => fk.PrincipalTable == "knowledge_categories");

        Assert.Equal(ReferentialAction.Restrict, categoryFk.OnDelete);
        Assert.Equal(["category_id"], categoryFk.Columns);
    }

    [Fact]
    public void Admin_article_keyword_filter_searches_only_title_not_content_json()
    {
        using var context = CreateContext();
        var repo = new EfKnowledgeAdminRepository(context);
        var sql = repo.BuildFilteredArticleQuery(context.KnowledgeArticles.AsNoTracking(),
                new KnowledgeAdminArticleFilter("Reading", "Reading", null, null, "Published", "updatedAt:desc"))
            .Select(article => article.Id)
            .ToQueryString();

        Assert.Contains("title", sql);
        Assert.Contains("ILIKE", sql);
        Assert.DoesNotContain("content_json", sql);
    }

    [Fact]
    public void Admin_category_and_tag_filters_generate_ilike_sql()
    {
        using var context = CreateContext();
        var repo = new EfKnowledgeAdminRepository(context);

        var categorySql = repo.BuildFilteredCategoryQuery(context.KnowledgeCategories.AsNoTracking(),
                new KnowledgeAdminCategoryFilter("Reading", true, "displayOrder:asc"))
            .Select(category => category.Id)
            .ToQueryString();
        Assert.Contains("ILIKE", categorySql);
        Assert.Contains("EXISTS", categorySql);

        var tagSql = repo.BuildFilteredTagQuery(context.KnowledgeTags.AsNoTracking(),
                new KnowledgeAdminTagFilter("part", "name:asc"))
            .Select(tag => tag.Id)
            .ToQueryString();
        Assert.Contains("ILIKE", tagSql);

        var mediaSql = repo.BuildFilteredMediaQuery(context.KnowledgeMediaAssets.AsNoTracking(),
                new KnowledgeAdminMediaFilter("cover", true, "createdAt:desc"))
            .Select(asset => asset.Id)
            .ToQueryString();
        Assert.Contains("ILIKE", mediaSql);
        Assert.Contains("NOT EXISTS", mediaSql);
    }

    [Fact]
    public void Knowledge_article_lifecycle_and_media_tag_sync_enforce_domain_invariants()
    {
        var now = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        var adminId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var coverId = Guid.NewGuid();
        var embeddedId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        var article = KnowledgeArticle.Create(
            "  Mẹo làm Reading Part 7  ",
            "  Meo-Lam-Reading-Part-7 ",
            "Tóm tắt ngắn",
            KnowledgeSkill.Reading,
            categoryId,
            coverId,
            "Ảnh bìa",
            """{"type":"doc","content":[]}""",
            "[]",
            0,
            [tagId, tagId],
            [embeddedId],
            adminId,
            now);

        Assert.Equal(KnowledgeArticleStatus.Draft, article.Status);
        Assert.Equal("meo-lam-reading-part-7", article.Slug);
        Assert.Equal(1, article.EstimatedReadMinutes);
        Assert.True(article.CanBeDeleted);
        Assert.Single(article.ArticleTags);
        Assert.Equal(2, article.MediaAssets.Count);
        Assert.Contains(article.MediaAssets, m => m.MediaAssetId == coverId);
        Assert.Contains(article.MediaAssets, m => m.MediaAssetId == embeddedId);

        article.Publish(adminId, now.AddMinutes(5));
        Assert.Equal(KnowledgeArticleStatus.Published, article.Status);
        Assert.Equal(now.AddMinutes(5), article.PublishedAtUtc);
        Assert.False(article.CanBeDeleted);
        Assert.Equal(2, article.Version);

        // Editing a published article is allowed and keeps Published status.
        article.Update(
            "Mẹo làm Reading Part 7 (Cập nhật)",
            "meo-lam-reading-part-7",
            "Tóm tắt cập nhật",
            KnowledgeSkill.Reading,
            categoryId,
            null,
            null,
            """{"type":"doc","content":[{"type":"paragraph"}]}""",
            """[{"id":"h1","level":2,"text":"Mục 1"}]""",
            4,
            [],
            [embeddedId],
            adminId,
            now.AddMinutes(10));

        Assert.Equal(KnowledgeArticleStatus.Published, article.Status);
        Assert.Empty(article.ArticleTags);
        Assert.Single(article.MediaAssets);
        Assert.Equal(3, article.Version);

        article.Unpublish(adminId, now.AddMinutes(15));
        Assert.Equal(KnowledgeArticleStatus.Draft, article.Status);
        Assert.True(article.CanBeDeleted);

        article.Archive(adminId, now.AddMinutes(20));
        Assert.Equal(KnowledgeArticleStatus.Archived, article.Status);
        Assert.True(article.CanBeDeleted);
    }

    private sealed class AddKnowledgeBaseProbe : AddKnowledgeBase
    {
        public IReadOnlyList<MigrationOperation> GetUpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }
    }
}

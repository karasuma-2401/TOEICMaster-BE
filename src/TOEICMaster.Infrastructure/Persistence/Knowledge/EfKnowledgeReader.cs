using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Knowledge;
using TOEICMaster.Application.Knowledge.Admin;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Knowledge;

public sealed class EfKnowledgeReader(ToeicDbContext db, TimeProvider timeProvider) : IKnowledgeReader
{
    public Task<Guid?> FindPublishedAsync(string slug, CancellationToken ct) => db.KnowledgeArticles
        .Where(a => a.Status == KnowledgeArticleStatus.Published && a.Slug == slug)
        .Select(a => (Guid?)a.Id).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<KnowledgeArticleSummaryDto>> RelatedAsync(Guid articleId, CancellationToken ct)
    {
        var currentCategory = db.KnowledgeArticles.Where(a => a.Id == articleId).Select(a => a.CategoryId);
        var tags = db.KnowledgeArticleTags.Where(t => t.ArticleId == articleId).Select(t => t.TagId);
        return await db.KnowledgeArticles.AsNoTracking().AsSingleQuery()
            .Where(a => a.Status == KnowledgeArticleStatus.Published && a.Id != articleId)
            .Select(a => new
            {
                Article = a,
                SameCategory = currentCategory.Contains(a.CategoryId),
                Matches = a.ArticleTags.Count(t => tags.Contains(t.TagId))
            })
            .Where(a => a.Matches > 0 || a.SameCategory)
            .OrderByDescending(a => a.Matches).ThenByDescending(a => a.SameCategory)
            .ThenByDescending(a => a.Article.PublishedAtUtc).ThenBy(a => a.Article.Id).Take(3)
            .Select(row => new KnowledgeArticleSummaryDto(row.Article.Id, row.Article.Title, row.Article.Slug,
                row.Article.Summary, row.Article.Skill.ToString(), row.Article.CategoryId,
                db.KnowledgeCategories.Where(c => c.Id == row.Article.CategoryId).Select(c => c.Name).First(),
                row.Article.CoverImageAssetId, null, row.Article.CoverImageAlt, row.Article.EstimatedReadMinutes,
                row.Article.Status.ToString(), row.Article.PublishedAtUtc,
                (from link in db.KnowledgeArticleTags
                 join tag in db.KnowledgeTags on link.TagId equals tag.Id
                 where link.ArticleId == row.Article.Id
                 orderby tag.Name
                 select new KnowledgeTagDto(tag.Id, tag.Name, tag.Slug)).ToArray(),
                row.Article.CreatedAtUtc, row.Article.UpdatedAtUtc, row.Article.Version)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<KnowledgeCategoryDto>> CategoriesAsync(CancellationToken ct) =>
        await db.KnowledgeCategories.AsNoTracking().OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Where(c => db.KnowledgeArticles.Any(a => a.CategoryId == c.Id && a.Status == KnowledgeArticleStatus.Published))
            .Select(c => new KnowledgeCategoryDto(c.Id, c.Name, c.Slug, c.Description, c.DisplayOrder,
                db.KnowledgeArticles.Count(a => a.CategoryId == c.Id && a.Status == KnowledgeArticleStatus.Published),
                c.CreatedAtUtc, c.UpdatedAtUtc, c.Version)).ToListAsync(ct);

    public Task<bool> IsPublicMediaAsync(Guid assetId, CancellationToken ct) => db.KnowledgeArticles.AnyAsync(a =>
        a.Status == KnowledgeArticleStatus.Published && (a.CoverImageAssetId == assetId || a.MediaAssets.Any(m => m.MediaAssetId == assetId)), ct);

    public async Task<SavedKnowledgeArticlePage> SavedAsync(Guid userId, int pageNumber, int pageSize,
        Guid? articleId, CancellationToken ct)
    {
        var bookmarks = db.Set<KnowledgeBookmark>().AsNoTracking()
            .Where(b => b.UserId == userId && (articleId == null || b.ArticleId == articleId));
        var totalCount = await bookmarks.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var items = await (from b in bookmarks
                           join a in db.KnowledgeArticles on b.ArticleId equals a.Id
                           orderby b.SavedAtUtc descending, b.ArticleId
                           select new SavedKnowledgeArticle(a.Id,
                               a.Status == KnowledgeArticleStatus.Published ? a.Title : "Lesson is no longer available",
                               a.Status == KnowledgeArticleStatus.Published ? "/knowledge/" + a.Slug : null,
                               a.Status == KnowledgeArticleStatus.Published))
            .Skip(checked((pageNumber - 1) * pageSize)).Take(pageSize).ToListAsync(ct);
        return new(items, pageNumber, pageSize, totalCount, totalPages, pageNumber < totalPages, pageNumber > 1);
    }

    public async Task SaveAsync(Guid userId, Guid articleId, CancellationToken ct)
    {
        // Lock publication state through the insert so concurrent unpublish/delete cannot create an invalid bookmark.
        var available = await db.Database.SqlQuery<bool>($"""
            WITH available AS (
                SELECT id FROM knowledge_articles WHERE id = {articleId}
                    AND status = {(short)KnowledgeArticleStatus.Published} FOR SHARE
            ), saved AS (
                INSERT INTO knowledge_bookmarks (user_id, article_id, saved_at_utc)
                SELECT {userId}, id, {timeProvider.GetUtcNow()} FROM available ON CONFLICT DO NOTHING
            )
            SELECT EXISTS (SELECT 1 FROM available) AS "Value"
            """).ToListAsync(ct);
        if (!available.Single())
            throw new KnowledgeNotFoundException("Lesson not found.");
    }

    public Task RemoveAsync(Guid userId, Guid articleId, CancellationToken ct) => db.Set<KnowledgeBookmark>()
        .Where(b => b.UserId == userId && b.ArticleId == articleId).ExecuteDeleteAsync(ct);
}

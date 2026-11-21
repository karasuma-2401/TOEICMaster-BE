using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Knowledge;
using TOEICMaster.Application.Knowledge.Admin;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Knowledge;

public sealed class EfKnowledgeReader(ToeicDbContext db, IKnowledgeAdminRepository admin) : IKnowledgeReader
{
    public Task<Guid?> FindPublishedAsync(string slug, CancellationToken ct) => db.KnowledgeArticles
        .Where(a => a.Status == KnowledgeArticleStatus.Published && a.Slug == slug)
        .Select(a => (Guid?)a.Id).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<KnowledgeArticleSummaryDto>> RelatedAsync(Guid articleId, CancellationToken ct)
    {
        var current = await db.KnowledgeArticles.AsNoTracking().SingleAsync(a => a.Id == articleId, ct);
        var tags = db.KnowledgeArticleTags.Where(t => t.ArticleId == articleId).Select(t => t.TagId);
        var ids = await db.KnowledgeArticles.Where(a => a.Status == KnowledgeArticleStatus.Published && a.Id != articleId)
            .Select(a => new { a.Id, a.CategoryId, a.PublishedAtUtc,
                Matches = a.ArticleTags.Count(t => tags.Contains(t.TagId)) })
            .Where(a => a.Matches > 0 || a.CategoryId == current.CategoryId)
            .OrderByDescending(a => a.Matches).ThenByDescending(a => a.CategoryId == current.CategoryId)
            .ThenByDescending(a => a.PublishedAtUtc).ThenBy(a => a.Id).Take(3).Select(a => a.Id).ToListAsync(ct);
        var result = new List<KnowledgeArticleSummaryDto>();
        foreach (var id in ids)
        {
            var a = await admin.GetArticleDetailAsync(id, ct);
            if (a is not { Status: "Published" }) continue;
            result.Add(new(a.Id, a.Title, a.Slug, a.Summary, a.Skill, a.CategoryId, a.CategoryName,
                a.CoverImageAssetId, a.CoverImageUrl, a.CoverImageAlt, a.EstimatedReadMinutes, a.Status,
                a.PublishedAtUtc, a.Tags, a.CreatedAtUtc, a.UpdatedAtUtc, a.Version));
        }
        return result;
    }

    public async Task<IReadOnlyList<KnowledgeCategoryDto>> CategoriesAsync(CancellationToken ct) =>
        await db.KnowledgeCategories.AsNoTracking().OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Where(c => db.KnowledgeArticles.Any(a => a.CategoryId == c.Id && a.Status == KnowledgeArticleStatus.Published))
            .Select(c => new KnowledgeCategoryDto(c.Id, c.Name, c.Slug, c.Description, c.DisplayOrder,
                db.KnowledgeArticles.Count(a => a.CategoryId == c.Id && a.Status == KnowledgeArticleStatus.Published),
                c.CreatedAtUtc, c.UpdatedAtUtc, c.Version)).ToListAsync(ct);

    public Task<bool> IsPublicMediaAsync(Guid assetId, CancellationToken ct) => db.KnowledgeArticles.AnyAsync(a =>
        a.Status == KnowledgeArticleStatus.Published && (a.CoverImageAssetId == assetId || a.MediaAssets.Any(m => m.MediaAssetId == assetId)), ct);

    public async Task<IReadOnlyList<SavedKnowledgeArticle>> SavedAsync(Guid userId, CancellationToken ct) =>
        await (from b in db.Set<KnowledgeBookmark>().AsNoTracking()
               join a in db.KnowledgeArticles on b.ArticleId equals a.Id
               where b.UserId == userId orderby b.SavedAtUtc descending
               select new SavedKnowledgeArticle(a.Id, a.Status == KnowledgeArticleStatus.Published ? a.Title : "Bài học không còn khả dụng",
                   "/knowledge/" + a.Slug, a.Status == KnowledgeArticleStatus.Published)).ToListAsync(ct);

    public async Task SaveAsync(Guid userId, Guid articleId, CancellationToken ct)
    {
        if (!await db.KnowledgeArticles.AnyAsync(a => a.Id == articleId && a.Status == KnowledgeArticleStatus.Published, ct))
            throw new KnowledgeNotFoundException("Không tìm thấy bài học.");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO knowledge_bookmarks (user_id, article_id, saved_at_utc) VALUES ({userId}, {articleId}, {DateTimeOffset.UtcNow}) ON CONFLICT DO NOTHING", ct);
    }

    public Task RemoveAsync(Guid userId, Guid articleId, CancellationToken ct) => db.Set<KnowledgeBookmark>()
        .Where(b => b.UserId == userId && b.ArticleId == articleId).ExecuteDeleteAsync(ct);
}

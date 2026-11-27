using TOEICMaster.Application.Knowledge;
using TOEICMaster.Application.Knowledge.Admin;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.TestDoubles;

public sealed class EmptyKnowledgeReader : IKnowledgeReader
{
    public Task<Guid?> FindPublishedAsync(string slug, CancellationToken ct) => Task.FromResult<Guid?>(null);
    public Task<IReadOnlyList<KnowledgeArticleSummaryDto>> RelatedAsync(Guid articleId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<KnowledgeArticleSummaryDto>>([]);
    public Task<IReadOnlyList<KnowledgeCategoryDto>> CategoriesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<KnowledgeCategoryDto>>([]);
    public Task<bool> IsPublicMediaAsync(Guid assetId, CancellationToken ct) => Task.FromResult(false);
    public Task<SavedKnowledgeArticlePage> SavedAsync(Guid userId, int pageNumber, int pageSize, Guid? articleId, CancellationToken ct) =>
        Task.FromResult(new SavedKnowledgeArticlePage([], pageNumber, pageSize, 0, 0, false, pageNumber > 1));
    public Task SaveAsync(Guid userId, Guid articleId, CancellationToken ct) => throw new KnowledgeNotFoundException("Lesson not found.");
    public Task RemoveAsync(Guid userId, Guid articleId, CancellationToken ct) => Task.CompletedTask;
}

public sealed class InMemoryKnowledgeAdminRepository : IKnowledgeAdminRepository
{
    public List<KnowledgeCategory> Categories { get; } = [];
    public HashSet<Guid> CategoriesWithArticles { get; } = [];
    public List<KnowledgeTag> Tags { get; } = [];
    public List<KnowledgeMediaAsset> MediaAssets { get; } = [];
    public List<KnowledgeArticle> Articles { get; } = [];

    public Task<KnowledgeCategoryPageDto> GetCategoriesAsync(KnowledgeAdminCategoryFilter filter,
        CancellationToken cancellationToken)
    {
        var items = Categories.Select(c => new KnowledgeCategoryDto(
            c.Id, c.Name, c.Slug, c.Description, c.DisplayOrder,
            CategoriesWithArticles.Contains(c.Id) ? 1 : Articles.Count(a => a.CategoryId == c.Id),
            c.CreatedAtUtc, c.UpdatedAtUtc, c.Version)).ToArray();
        var totalPages = items.Length == 0 ? 0 : (int)Math.Ceiling(items.Length / (double)filter.PageSize);
        return Task.FromResult(new KnowledgeCategoryPageDto(items, filter.PageNumber, filter.PageSize, items.Length,
            totalPages, filter.PageNumber < totalPages, filter.PageNumber > 1));
    }

    public Task<KnowledgeCategory?> FindCategoryForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.SingleOrDefault(c => c.Id == id));

    public Task<bool> CategoryExistsAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.Any(c => c.Id == id));

    public Task<bool> CategorySlugExistsAsync(string slug, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.Any(c => c.Slug == slug && (!excludingId.HasValue || c.Id != excludingId.Value)));

    public Task<bool> CategoryHasArticlesAsync(Guid categoryId, CancellationToken cancellationToken) =>
        Task.FromResult(CategoriesWithArticles.Contains(categoryId) || Articles.Any(a => a.CategoryId == categoryId));

    public Task<int> GetCategoryArticleCountAsync(Guid categoryId, CancellationToken cancellationToken) =>
        Task.FromResult(Articles.Count(a => a.CategoryId == categoryId));

    public Task AddCategoryAsync(KnowledgeCategory category, CancellationToken cancellationToken)
    {
        Categories.Add(category);
        return Task.CompletedTask;
    }

    public void RemoveCategory(KnowledgeCategory category) => Categories.Remove(category);

    public Task<KnowledgeTagPageDto> GetTagsPageAsync(KnowledgeAdminTagFilter filter,
        CancellationToken cancellationToken)
    {
        var items = Tags.Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(t => new KnowledgeTagDto(t.Id, t.Name, t.Slug)).ToArray();
        var totalPages = Tags.Count == 0 ? 0 : (int)Math.Ceiling(Tags.Count / (double)filter.PageSize);
        return Task.FromResult(new KnowledgeTagPageDto(items, filter.PageNumber, filter.PageSize, Tags.Count,
            totalPages, filter.PageNumber < totalPages, filter.PageNumber > 1));
    }

    public Task<IReadOnlyList<KnowledgeTag>> FindTagsBySlugsAsync(IReadOnlyCollection<string> slugs,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<KnowledgeTag>>(Tags.Where(t => slugs.Contains(t.Slug)).ToArray());

    public Task AddTagsAsync(IEnumerable<KnowledgeTag> tags, CancellationToken cancellationToken)
    {
        Tags.AddRange(tags);
        return Task.CompletedTask;
    }

    public Task<KnowledgeMediaAssetPageDto> GetMediaAssetPageAsync(KnowledgeAdminMediaFilter filter,
        CancellationToken cancellationToken)
    {
        var items = MediaAssets.Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(m => new KnowledgeMediaAssetSummaryDto(m.Id, m.OriginalFileName, m.ContentType, m.ByteLength,
                KnowledgeEnumParsing.ToAdminMediaUrl(m.Id),
                Articles.Count(a => a.CoverImageAssetId == m.Id || a.MediaAssets.Any(l => l.MediaAssetId == m.Id)),
                m.CreatedAtUtc)).ToArray();
        var totalPages = MediaAssets.Count == 0 ? 0 : (int)Math.Ceiling(MediaAssets.Count / (double)filter.PageSize);
        return Task.FromResult(new KnowledgeMediaAssetPageDto(items, filter.PageNumber, filter.PageSize,
            MediaAssets.Count, totalPages, filter.PageNumber < totalPages, filter.PageNumber > 1));
    }

    public Task<KnowledgeMediaAsset?> FindMediaAssetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(MediaAssets.SingleOrDefault(m => m.Id == id));

    public Task<KnowledgeMediaAsset?> FindMediaAssetForDeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(MediaAssets.SingleOrDefault(m => m.Id == id));

    public Task<bool> IsMediaAssetUsedAsync(Guid assetId, CancellationToken cancellationToken) =>
        Task.FromResult(Articles.Any(a => a.CoverImageAssetId == assetId ||
                                          a.MediaAssets.Any(link => link.MediaAssetId == assetId)));

    public Task<IReadOnlySet<Guid>> FindExistingMediaAssetIdsAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(MediaAssets.Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToHashSet());

    public Task AddMediaAssetAsync(KnowledgeMediaAsset asset, CancellationToken cancellationToken)
    {
        MediaAssets.Add(asset);
        return Task.CompletedTask;
    }

    public void RemoveMediaAsset(KnowledgeMediaAsset asset) => MediaAssets.Remove(asset);

    public Task<KnowledgeArticlePageDto> GetArticlePageAsync(KnowledgeAdminArticleFilter filter,
        CancellationToken cancellationToken) =>
        Task.FromResult(new KnowledgeArticlePageDto([], filter.PageNumber, filter.PageSize, 0, 0, false, false));

    public Task<KnowledgeArticleDetailDto?> GetArticleDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var article = Articles.SingleOrDefault(a => a.Id == id);
        if (article is null)
            return Task.FromResult<KnowledgeArticleDetailDto?>(null);
        var categoryName = Categories.SingleOrDefault(c => c.Id == article.CategoryId)?.Name ?? string.Empty;
        var tagDtos = article.ArticleTags
            .Select(link => Tags.Single(t => t.Id == link.TagId))
            .Select(t => new KnowledgeTagDto(t.Id, t.Name, t.Slug))
            .ToArray();
        return Task.FromResult<KnowledgeArticleDetailDto?>(new KnowledgeArticleDetailDto(
            article.Id, article.Title, article.Slug, article.Summary, article.Skill.ToString(),
            article.CategoryId, categoryName, article.CoverImageAssetId,
            article.CoverImageAssetId is { } cid ? KnowledgeEnumParsing.ToAdminMediaUrl(cid) : null,
            article.CoverImageAlt, KnowledgeContentProcessor.ParseContent(article.ContentJson),
            KnowledgeContentProcessor.DeserializeToc(article.TocJson), article.EstimatedReadMinutes,
            article.Status.ToString(), article.PublishedAtUtc, tagDtos, article.CreatedAtUtc,
            article.UpdatedAtUtc, article.Version));
    }

    public Task<KnowledgeArticle?> FindArticleForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Articles.SingleOrDefault(a => a.Id == id));

    public Task<bool> ArticleSlugExistsAsync(string slug, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(Articles.Any(a => a.Slug == slug && (!excludingId.HasValue || a.Id != excludingId.Value)));

    public Task AddArticleAsync(KnowledgeArticle article, CancellationToken cancellationToken)
    {
        Articles.Add(article);
        return Task.CompletedTask;
    }

    public void RemoveArticle(KnowledgeArticle article) => Articles.Remove(article);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class InMemoryKnowledgeMediaStore : IKnowledgeMediaStore
{
    public Dictionary<string, (byte[] Content, string ContentType)> Objects { get; } = new(StringComparer.Ordinal);

    public Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        Objects[objectKey] = (content, contentType);
        return Task.CompletedTask;
    }

    public Task<byte[]> DownloadAsync(string objectKey, CancellationToken cancellationToken) =>
        Objects.TryGetValue(objectKey, out var entry)
            ? Task.FromResult(entry.Content)
            : throw new KnowledgeNotFoundException("Image not found.");

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        Objects.Remove(objectKey);
        return Task.CompletedTask;
    }
}

using Microsoft.EntityFrameworkCore;
using Npgsql;
using TOEICMaster.Application.Knowledge;
using TOEICMaster.Application.Knowledge.Admin;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Knowledge;

public sealed class EfKnowledgeAdminRepository(ToeicDbContext context) : IKnowledgeAdminRepository
{
    public async Task<KnowledgeCategoryPageDto> GetCategoriesAsync(KnowledgeAdminCategoryFilter filter,
        CancellationToken cancellationToken)
    {
        var query = BuildFilteredCategoryQuery(context.KnowledgeCategories.AsNoTracking(), filter);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)filter.PageSize);
        var offset = checked((filter.PageNumber - 1) * filter.PageSize);

        var categories = await ApplyCategorySort(query, filter.Sort)
            .Skip(offset)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        if (categories.Count == 0)
        {
            return new KnowledgeCategoryPageDto([], filter.PageNumber, filter.PageSize, totalCount, totalPages,
                filter.PageNumber < totalPages, filter.PageNumber > 1);
        }

        var categoryIds = categories.Select(category => category.Id).ToArray();
        var counts = await context.KnowledgeArticles.AsNoTracking()
            .Where(article => categoryIds.Contains(article.CategoryId))
            .GroupBy(article => article.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken);

        var items = categories.Select(category => new KnowledgeCategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.DisplayOrder,
            counts.GetValueOrDefault(category.Id),
            category.CreatedAtUtc,
            category.UpdatedAtUtc,
            category.Version)).ToArray();

        return new KnowledgeCategoryPageDto(items, filter.PageNumber, filter.PageSize, totalCount, totalPages,
            filter.PageNumber < totalPages, filter.PageNumber > 1);
    }

    public Task<KnowledgeCategory?> FindCategoryForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.KnowledgeCategories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);

    public Task<bool> CategoryExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.KnowledgeCategories.AsNoTracking().AnyAsync(category => category.Id == id, cancellationToken);

    public Task<bool> CategorySlugExistsAsync(string slug, Guid? excludingId, CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return context.KnowledgeCategories.AsNoTracking().AnyAsync(
            category => category.Slug == normalized && (!excludingId.HasValue || category.Id != excludingId.Value),
            cancellationToken);
    }

    public Task<bool> CategoryHasArticlesAsync(Guid categoryId, CancellationToken cancellationToken) =>
        context.KnowledgeArticles.AsNoTracking().AnyAsync(article => article.CategoryId == categoryId, cancellationToken);

    public Task<int> GetCategoryArticleCountAsync(Guid categoryId, CancellationToken cancellationToken) =>
        context.KnowledgeArticles.AsNoTracking().CountAsync(article => article.CategoryId == categoryId,
            cancellationToken);

    public Task AddCategoryAsync(KnowledgeCategory category, CancellationToken cancellationToken)
    {
        context.KnowledgeCategories.Add(category);
        return Task.CompletedTask;
    }

    public void RemoveCategory(KnowledgeCategory category) => context.KnowledgeCategories.Remove(category);

    public async Task<KnowledgeTagPageDto> GetTagsPageAsync(KnowledgeAdminTagFilter filter,
        CancellationToken cancellationToken)
    {
        var query = BuildFilteredTagQuery(context.KnowledgeTags.AsNoTracking(), filter);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)filter.PageSize);
        var offset = checked((filter.PageNumber - 1) * filter.PageSize);

        var items = await ApplyTagSort(query, filter.Sort)
            .Skip(offset)
            .Take(filter.PageSize)
            .Select(tag => new KnowledgeTagDto(tag.Id, tag.Name, tag.Slug))
            .ToListAsync(cancellationToken);

        return new KnowledgeTagPageDto(items, filter.PageNumber, filter.PageSize, totalCount, totalPages,
            filter.PageNumber < totalPages, filter.PageNumber > 1);
    }

    public async Task<IReadOnlyList<KnowledgeTag>> FindTagsBySlugsAsync(IReadOnlyCollection<string> slugs,
        CancellationToken cancellationToken)
    {
        if (slugs.Count == 0)
            return [];
        return await context.KnowledgeTags.AsNoTracking()
            .Where(tag => slugs.Contains(tag.Slug))
            .ToListAsync(cancellationToken);
    }

    public Task AddTagsAsync(IEnumerable<KnowledgeTag> tags, CancellationToken cancellationToken)
    {
        context.KnowledgeTags.AddRange(tags);
        return Task.CompletedTask;
    }

    public async Task<KnowledgeMediaAssetPageDto> GetMediaAssetPageAsync(KnowledgeAdminMediaFilter filter,
        CancellationToken cancellationToken)
    {
        var query = BuildFilteredMediaQuery(context.KnowledgeMediaAssets.AsNoTracking(), filter);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)filter.PageSize);
        var offset = checked((filter.PageNumber - 1) * filter.PageSize);

        var assets = await ApplyMediaSort(query, filter.Sort)
            .Skip(offset)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        if (assets.Count == 0)
        {
            return new KnowledgeMediaAssetPageDto([], filter.PageNumber, filter.PageSize, totalCount, totalPages,
                filter.PageNumber < totalPages, filter.PageNumber > 1);
        }

        var assetIds = assets.Select(asset => asset.Id).ToArray();
        var usageCounts = await context.KnowledgeArticleMediaAssets.AsNoTracking()
            .Where(link => assetIds.Contains(link.MediaAssetId))
            .GroupBy(link => link.MediaAssetId)
            .Select(group => new { MediaAssetId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.MediaAssetId, item => item.Count, cancellationToken);

        var items = assets.Select(asset => new KnowledgeMediaAssetSummaryDto(
            asset.Id,
            asset.OriginalFileName,
            asset.ContentType,
            asset.ByteLength,
            KnowledgeEnumParsing.ToAdminMediaUrl(asset.Id),
            usageCounts.GetValueOrDefault(asset.Id),
            asset.CreatedAtUtc)).ToArray();

        return new KnowledgeMediaAssetPageDto(items, filter.PageNumber, filter.PageSize, totalCount, totalPages,
            filter.PageNumber < totalPages, filter.PageNumber > 1);
    }

    public Task<KnowledgeMediaAsset?> FindMediaAssetAsync(Guid id, CancellationToken cancellationToken) =>
        context.KnowledgeMediaAssets.AsNoTracking().SingleOrDefaultAsync(asset => asset.Id == id, cancellationToken);

    public Task<KnowledgeMediaAsset?> FindMediaAssetForDeleteAsync(Guid id, CancellationToken cancellationToken) =>
        context.KnowledgeMediaAssets.SingleOrDefaultAsync(asset => asset.Id == id, cancellationToken);

    public async Task<bool> IsMediaAssetUsedAsync(Guid assetId, CancellationToken cancellationToken) =>
        await context.KnowledgeArticleMediaAssets.AsNoTracking()
            .AnyAsync(link => link.MediaAssetId == assetId, cancellationToken) ||
        await context.KnowledgeArticles.AsNoTracking()
            .AnyAsync(article => article.CoverImageAssetId == assetId, cancellationToken);

    public async Task<IReadOnlySet<Guid>> FindExistingMediaAssetIdsAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return new HashSet<Guid>();
        var existing = await context.KnowledgeMediaAssets.AsNoTracking()
            .Where(asset => ids.Contains(asset.Id))
            .Select(asset => asset.Id)
            .ToListAsync(cancellationToken);
        return existing.ToHashSet();
    }

    public Task AddMediaAssetAsync(KnowledgeMediaAsset asset, CancellationToken cancellationToken)
    {
        context.KnowledgeMediaAssets.Add(asset);
        return Task.CompletedTask;
    }

    public void RemoveMediaAsset(KnowledgeMediaAsset asset) => context.KnowledgeMediaAssets.Remove(asset);

    public async Task<KnowledgeArticlePageDto> GetArticlePageAsync(KnowledgeAdminArticleFilter filter,
        CancellationToken cancellationToken)
    {
        var query = BuildFilteredArticleQuery(context.KnowledgeArticles.AsNoTracking(), filter);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)filter.PageSize);
        var offset = checked((filter.PageNumber - 1) * filter.PageSize);

        var rows = await ApplySort(query, filter.Sort)
            .Skip(offset)
            .Take(filter.PageSize)
            .Select(article => new
            {
                article.Id,
                article.Title,
                article.Slug,
                article.Summary,
                article.Skill,
                article.CategoryId,
                article.CoverImageAssetId,
                article.CoverImageAlt,
                article.EstimatedReadMinutes,
                article.Status,
                article.PublishedAtUtc,
                article.CreatedAtUtc,
                article.UpdatedAtUtc,
                article.Version
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new KnowledgeArticlePageDto([], filter.PageNumber, filter.PageSize, totalCount, totalPages,
                filter.PageNumber < totalPages, filter.PageNumber > 1);
        }

        var categoryIds = rows.Select(row => row.CategoryId).Distinct().ToArray();
        var categoryNames = await context.KnowledgeCategories.AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        var articleIds = rows.Select(row => row.Id).ToArray();
        var tagLinks = await (from link in context.KnowledgeArticleTags.AsNoTracking()
                              join tag in context.KnowledgeTags.AsNoTracking() on link.TagId equals tag.Id
                              where articleIds.Contains(link.ArticleId)
                              orderby tag.Name
                              select new { link.ArticleId, tag.Id, tag.Name, tag.Slug })
            .ToListAsync(cancellationToken);

        var tagsByArticle = tagLinks
            .GroupBy(item => item.ArticleId)
            .ToDictionary(group => group.Key,
                group => (IReadOnlyList<KnowledgeTagDto>)group
                    .Select(item => new KnowledgeTagDto(item.Id, item.Name, item.Slug)).ToArray());

        var items = rows.Select(row => new KnowledgeArticleSummaryDto(
            row.Id,
            row.Title,
            row.Slug,
            row.Summary,
            row.Skill.ToString(),
            row.CategoryId,
            categoryNames.GetValueOrDefault(row.CategoryId, string.Empty),
            row.CoverImageAssetId,
            row.CoverImageAssetId is { } coverId ? KnowledgeEnumParsing.ToAdminMediaUrl(coverId) : null,
            row.CoverImageAlt,
            row.EstimatedReadMinutes,
            row.Status.ToString(),
            row.PublishedAtUtc,
            tagsByArticle.GetValueOrDefault(row.Id, []),
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.Version)).ToArray();

        return new KnowledgeArticlePageDto(items, filter.PageNumber, filter.PageSize, totalCount, totalPages,
            filter.PageNumber < totalPages, filter.PageNumber > 1);
    }

    public async Task<KnowledgeArticleDetailDto?> GetArticleDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var article = await context.KnowledgeArticles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (article is null)
            return null;

        var categoryName = await context.KnowledgeCategories.AsNoTracking()
            .Where(category => category.Id == article.CategoryId)
            .Select(category => category.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;

        var tags = await (from link in context.KnowledgeArticleTags.AsNoTracking()
                          join tag in context.KnowledgeTags.AsNoTracking() on link.TagId equals tag.Id
                          where link.ArticleId == article.Id
                          orderby tag.Name
                          select new KnowledgeTagDto(tag.Id, tag.Name, tag.Slug))
            .ToListAsync(cancellationToken);

        return new KnowledgeArticleDetailDto(
            article.Id,
            article.Title,
            article.Slug,
            article.Summary,
            article.Skill.ToString(),
            article.CategoryId,
            categoryName,
            article.CoverImageAssetId,
            article.CoverImageAssetId is { } coverId ? KnowledgeEnumParsing.ToAdminMediaUrl(coverId) : null,
            article.CoverImageAlt,
            KnowledgeContentProcessor.ParseContent(article.ContentJson),
            KnowledgeContentProcessor.DeserializeToc(article.TocJson),
            article.EstimatedReadMinutes,
            article.Status.ToString(),
            article.PublishedAtUtc,
            tags,
            article.CreatedAtUtc,
            article.UpdatedAtUtc,
            article.Version);
    }

    public Task<KnowledgeArticle?> FindArticleForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.KnowledgeArticles
            .Include(article => article.ArticleTags)
            .Include(article => article.MediaAssets)
            .SingleOrDefaultAsync(article => article.Id == id, cancellationToken);

    public Task<bool> ArticleSlugExistsAsync(string slug, Guid? excludingId, CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return context.KnowledgeArticles.AsNoTracking().AnyAsync(
            article => article.Slug == normalized && (!excludingId.HasValue || article.Id != excludingId.Value),
            cancellationToken);
    }

    public Task AddArticleAsync(KnowledgeArticle article, CancellationToken cancellationToken)
    {
        context.KnowledgeArticles.Add(article);
        return Task.CompletedTask;
    }

    public void RemoveArticle(KnowledgeArticle article) => context.KnowledgeArticles.Remove(article);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new KnowledgeConflictException("Data was updated by another user. Please reload the data.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            if (pg.SqlState == PostgresErrorCodes.UniqueViolation)
                throw new KnowledgeConflictException("Duplicate unique key (slug or media file).");
            if (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                throw new KnowledgeConflictException(
                    "Cannot complete the operation because related article or media data still exists.");
            throw;
        }
    }

    internal IQueryable<KnowledgeCategory> BuildFilteredCategoryQuery(IQueryable<KnowledgeCategory> source,
        KnowledgeAdminCategoryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var escaped = EscapeLike(filter.Query.Trim());
            source = source.Where(category => EF.Functions.ILike(category.Name, $"%{escaped}%", "\\") ||
                                              EF.Functions.ILike(category.Slug, $"%{escaped}%", "\\"));
        }

        if (filter.HasArticles.HasValue)
        {
            source = filter.HasArticles.Value
                ? source.Where(category => context.KnowledgeArticles.Any(article => article.CategoryId == category.Id))
                : source.Where(category => !context.KnowledgeArticles.Any(article => article.CategoryId == category.Id));
        }

        return source;
    }

    internal IQueryable<KnowledgeTag> BuildFilteredTagQuery(IQueryable<KnowledgeTag> source,
        KnowledgeAdminTagFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var escaped = EscapeLike(filter.Query.Trim());
            source = source.Where(tag => EF.Functions.ILike(tag.Name, $"%{escaped}%", "\\") ||
                                         EF.Functions.ILike(tag.Slug, $"%{escaped}%", "\\"));
        }

        return source;
    }

    internal IQueryable<KnowledgeMediaAsset> BuildFilteredMediaQuery(IQueryable<KnowledgeMediaAsset> source,
        KnowledgeAdminMediaFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var escaped = EscapeLike(filter.Query.Trim());
            source = source.Where(asset => EF.Functions.ILike(asset.OriginalFileName, $"%{escaped}%", "\\"));
        }

        if (filter.UnusedOnly.HasValue)
        {
            source = filter.UnusedOnly.Value
                ? source.Where(asset =>
                    !context.KnowledgeArticleMediaAssets.Any(link => link.MediaAssetId == asset.Id) &&
                    !context.KnowledgeArticles.Any(article => article.CoverImageAssetId == asset.Id))
                : source.Where(asset =>
                    context.KnowledgeArticleMediaAssets.Any(link => link.MediaAssetId == asset.Id) ||
                    context.KnowledgeArticles.Any(article => article.CoverImageAssetId == asset.Id));
        }

        return source;
    }

    private static IOrderedQueryable<KnowledgeMediaAsset> ApplyMediaSort(IQueryable<KnowledgeMediaAsset> query,
        string? sort) =>
        sort?.Trim().ToLowerInvariant() switch
        {
            "createdat:asc" => query.OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.Id),
            "filename:asc" => query.OrderBy(a => a.OriginalFileName).ThenBy(a => a.Id),
            "filename:desc" => query.OrderByDescending(a => a.OriginalFileName).ThenBy(a => a.Id),
            "bytelength:desc" => query.OrderByDescending(a => a.ByteLength).ThenBy(a => a.Id),
            "bytelength:asc" => query.OrderBy(a => a.ByteLength).ThenBy(a => a.Id),
            _ => query.OrderByDescending(a => a.CreatedAtUtc).ThenBy(a => a.Id)
        };

    private static IOrderedQueryable<KnowledgeCategory> ApplyCategorySort(IQueryable<KnowledgeCategory> query,
        string? sort) =>
        sort?.Trim().ToLowerInvariant() switch
        {
            "displayorder:desc" => query.OrderByDescending(c => c.DisplayOrder).ThenBy(c => c.Name).ThenBy(c => c.Id),
            "name:asc" => query.OrderBy(c => c.Name).ThenBy(c => c.Id),
            "name:desc" => query.OrderByDescending(c => c.Name).ThenBy(c => c.Id),
            "updatedat:desc" => query.OrderByDescending(c => c.UpdatedAtUtc).ThenBy(c => c.Id),
            "updatedat:asc" => query.OrderBy(c => c.UpdatedAtUtc).ThenBy(c => c.Id),
            "createdat:desc" => query.OrderByDescending(c => c.CreatedAtUtc).ThenBy(c => c.Id),
            "createdat:asc" => query.OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id),
            _ => query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ThenBy(c => c.Id)
        };

    private static IOrderedQueryable<KnowledgeTag> ApplyTagSort(IQueryable<KnowledgeTag> query, string? sort) =>
        sort?.Trim().ToLowerInvariant() switch
        {
            "name:desc" => query.OrderByDescending(t => t.Name).ThenBy(t => t.Id),
            "createdat:desc" => query.OrderByDescending(t => t.CreatedAtUtc).ThenBy(t => t.Id),
            "createdat:asc" => query.OrderBy(t => t.CreatedAtUtc).ThenBy(t => t.Id),
            _ => query.OrderBy(t => t.Name).ThenBy(t => t.Id)
        };

    internal IQueryable<KnowledgeArticle> BuildFilteredArticleQuery(IQueryable<KnowledgeArticle> source,
        KnowledgeAdminArticleFilter filter)
    {
        // Keyword filter intentionally searches ONLY on article Title (never full-scans content_json).
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var escaped = EscapeLike(filter.Query.Trim());
            source = source.Where(article => EF.Functions.ILike(article.Title, $"%{escaped}%", "\\"));
        }

        if (!string.IsNullOrWhiteSpace(filter.Skill) &&
            KnowledgeEnumParsing.TryParseSkill(filter.Skill, out var skill))
        {
            source = source.Where(article => article.Skill == skill);
        }

        if (filter.CategoryId.HasValue)
            source = source.Where(article => article.CategoryId == filter.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            KnowledgeEnumParsing.TryParseStatus(filter.Status, out var status))
        {
            source = source.Where(article => article.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tagSlug = KnowledgeSlug.Generate(filter.Tag, 64);
            var tagRaw = filter.Tag.Trim();
            source = source.Where(article =>
                context.KnowledgeArticleTags.Any(link =>
                    link.ArticleId == article.Id &&
                    context.KnowledgeTags.Any(tag =>
                        tag.Id == link.TagId &&
                        (tag.Slug == tagSlug || EF.Functions.ILike(tag.Name, tagRaw)))));
        }

        return source;
    }

    private static IOrderedQueryable<KnowledgeArticle> ApplySort(IQueryable<KnowledgeArticle> query, string? sort) =>
        sort?.Trim().ToLowerInvariant() switch
        {
            "updatedat:asc" => query.OrderBy(a => a.UpdatedAtUtc).ThenBy(a => a.Id),
            "createdat:desc" => query.OrderByDescending(a => a.CreatedAtUtc).ThenBy(a => a.Id),
            "createdat:asc" => query.OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.Id),
            "publishedat:desc" => query.OrderByDescending(a => a.PublishedAtUtc ?? DateTimeOffset.MinValue)
                .ThenByDescending(a => a.UpdatedAtUtc).ThenBy(a => a.Id),
            "publishedat:asc" => query.OrderBy(a => a.PublishedAtUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(a => a.UpdatedAtUtc).ThenBy(a => a.Id),
            "title:asc" => query.OrderBy(a => a.Title).ThenBy(a => a.Id),
            "title:desc" => query.OrderByDescending(a => a.Title).ThenBy(a => a.Id),
            _ => query.OrderByDescending(a => a.UpdatedAtUtc).ThenBy(a => a.Id)
        };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}

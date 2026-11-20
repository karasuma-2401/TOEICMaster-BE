using System.Text.Json;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Knowledge.Admin;

public sealed record KnowledgeCategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int DisplayOrder,
    int ArticleCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Version);

public sealed record KnowledgeCategoryPageDto(
    IReadOnlyList<KnowledgeCategoryDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public sealed record KnowledgeAdminCategoryFilter(
    string? Query,
    bool? HasArticles,
    string? Sort,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record KnowledgeTagDto(Guid Id, string Name, string Slug);

public sealed record KnowledgeTagPageDto(
    IReadOnlyList<KnowledgeTagDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public sealed record KnowledgeAdminTagFilter(
    string? Query,
    string? Sort,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record KnowledgeMediaAssetDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long ByteLength,
    string AdminUrl,
    DateTimeOffset CreatedAtUtc);

public sealed record KnowledgeMediaAssetSummaryDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long ByteLength,
    string AdminUrl,
    int UsageCount,
    DateTimeOffset CreatedAtUtc);

public sealed record KnowledgeMediaAssetPageDto(
    IReadOnlyList<KnowledgeMediaAssetSummaryDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public sealed record KnowledgeAdminMediaFilter(
    string? Query,
    bool? UnusedOnly,
    string? Sort,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record KnowledgeMediaBinary(byte[] Data, string ContentType);

public sealed record KnowledgeTocItemDto(string Id, int Level, string Text);

public sealed record KnowledgeArticleSummaryDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string Skill,
    Guid CategoryId,
    string CategoryName,
    Guid? CoverImageAssetId,
    string? CoverImageUrl,
    string? CoverImageAlt,
    int EstimatedReadMinutes,
    string Status,
    DateTimeOffset? PublishedAtUtc,
    IReadOnlyList<KnowledgeTagDto> Tags,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Version);

public sealed record KnowledgeArticleDetailDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string Skill,
    Guid CategoryId,
    string CategoryName,
    Guid? CoverImageAssetId,
    string? CoverImageUrl,
    string? CoverImageAlt,
    JsonElement Content,
    IReadOnlyList<KnowledgeTocItemDto> Toc,
    int EstimatedReadMinutes,
    string Status,
    DateTimeOffset? PublishedAtUtc,
    IReadOnlyList<KnowledgeTagDto> Tags,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Version);

public sealed record KnowledgeArticlePageDto(
    IReadOnlyList<KnowledgeArticleSummaryDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public sealed record KnowledgeAdminArticleFilter(
    string? Query,
    string? Skill,
    Guid? CategoryId,
    string? Tag,
    string? Status,
    string? Sort,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record KnowledgeCategoryInput(string Name, string? Slug, string? Description, int DisplayOrder = 0);

public sealed record KnowledgeArticleInput(
    string Title,
    string? Slug,
    string Summary,
    string Skill,
    Guid CategoryId,
    Guid? CoverImageAssetId,
    string? CoverImageAlt,
    JsonElement Content,
    IReadOnlyList<string>? Tags);

public interface IKnowledgeAdminRepository
{
    Task<KnowledgeCategoryPageDto> GetCategoriesAsync(KnowledgeAdminCategoryFilter filter,
        CancellationToken cancellationToken);
    Task<KnowledgeCategory?> FindCategoryForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CategoryExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CategorySlugExistsAsync(string slug, Guid? excludingId, CancellationToken cancellationToken);
    Task<bool> CategoryHasArticlesAsync(Guid categoryId, CancellationToken cancellationToken);
    Task<int> GetCategoryArticleCountAsync(Guid categoryId, CancellationToken cancellationToken);
    Task AddCategoryAsync(KnowledgeCategory category, CancellationToken cancellationToken);
    void RemoveCategory(KnowledgeCategory category);

    Task<KnowledgeTagPageDto> GetTagsPageAsync(KnowledgeAdminTagFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<KnowledgeTag>> FindTagsBySlugsAsync(IReadOnlyCollection<string> slugs,
        CancellationToken cancellationToken);
    Task AddTagsAsync(IEnumerable<KnowledgeTag> tags, CancellationToken cancellationToken);

    Task<KnowledgeMediaAssetPageDto> GetMediaAssetPageAsync(KnowledgeAdminMediaFilter filter,
        CancellationToken cancellationToken);
    Task<KnowledgeMediaAsset?> FindMediaAssetAsync(Guid id, CancellationToken cancellationToken);
    Task<KnowledgeMediaAsset?> FindMediaAssetForDeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> IsMediaAssetUsedAsync(Guid assetId, CancellationToken cancellationToken);
    Task<IReadOnlySet<Guid>> FindExistingMediaAssetIdsAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
    Task AddMediaAssetAsync(KnowledgeMediaAsset asset, CancellationToken cancellationToken);
    void RemoveMediaAsset(KnowledgeMediaAsset asset);

    Task<KnowledgeArticlePageDto> GetArticlePageAsync(KnowledgeAdminArticleFilter filter,
        CancellationToken cancellationToken);
    Task<KnowledgeArticleDetailDto?> GetArticleDetailAsync(Guid id, CancellationToken cancellationToken);
    Task<KnowledgeArticle?> FindArticleForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ArticleSlugExistsAsync(string slug, Guid? excludingId, CancellationToken cancellationToken);
    Task AddArticleAsync(KnowledgeArticle article, CancellationToken cancellationToken);
    void RemoveArticle(KnowledgeArticle article);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IKnowledgeMediaStore
{
    Task UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken);
    Task<byte[]> DownloadAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

internal static class KnowledgePagination
{
    public static bool CanRepresentOffset(int pageNumber, int pageSize) =>
        (pageNumber - 1L) * pageSize <= int.MaxValue;
}

public static class KnowledgeEnumParsing
{
    public static bool TryParseSkill(string? value, out KnowledgeSkill skill) =>
        Enum.TryParse(value?.Trim(), ignoreCase: true, out skill) && Enum.IsDefined(skill);

    public static bool TryParseStatus(string? value, out KnowledgeArticleStatus status) =>
        Enum.TryParse(value?.Trim(), ignoreCase: true, out status) && Enum.IsDefined(status);

    public static string ToAdminMediaUrl(Guid assetId) => $"/api/v1/admin/knowledge/media/{assetId}";
}

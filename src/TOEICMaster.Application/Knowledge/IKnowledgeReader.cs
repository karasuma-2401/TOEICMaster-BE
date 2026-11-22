using TOEICMaster.Application.Knowledge.Admin;

namespace TOEICMaster.Application.Knowledge;

public sealed record SavedKnowledgeArticle(Guid ArticleId, string Title, string Url, bool Available);

public interface IKnowledgeReader
{
    Task<Guid?> FindPublishedAsync(string slug, CancellationToken ct);
    Task<IReadOnlyList<KnowledgeArticleSummaryDto>> RelatedAsync(Guid articleId, CancellationToken ct);
    Task<IReadOnlyList<KnowledgeCategoryDto>> CategoriesAsync(CancellationToken ct);
    Task<bool> IsPublicMediaAsync(Guid assetId, CancellationToken ct);
    Task<IReadOnlyList<SavedKnowledgeArticle>> SavedAsync(Guid userId, CancellationToken ct);
    Task SaveAsync(Guid userId, Guid articleId, CancellationToken ct);
    Task RemoveAsync(Guid userId, Guid articleId, CancellationToken ct);
}

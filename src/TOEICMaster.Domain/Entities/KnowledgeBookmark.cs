namespace TOEICMaster.Domain.Entities;

public sealed class KnowledgeBookmark
{
    public Guid UserId { get; set; }
    public Guid ArticleId { get; set; }
    public DateTimeOffset SavedAtUtc { get; set; }
}

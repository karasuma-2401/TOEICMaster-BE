namespace TOEICMaster.Domain.Entities;

public enum KnowledgeSkill : short
{
    General = 0,
    Listening = 1,
    Reading = 2,
    Speaking = 3,
    Writing = 4
}

public enum KnowledgeArticleStatus : short
{
    Draft = 1,
    Published = 2,
    Archived = 3
}

public sealed class KnowledgeArticle
{
    private readonly List<KnowledgeArticleTag> _articleTags = [];
    private readonly List<KnowledgeArticleMediaAsset> _mediaAssets = [];

    private KnowledgeArticle()
    {
    }

    private KnowledgeArticle(Guid id, string title, string slug, string summary, KnowledgeSkill skill,
        Guid categoryId, Guid? coverImageAssetId, string? coverImageAlt, string contentJson, string tocJson,
        int estimatedReadMinutes, Guid actorUserId, DateTimeOffset now)
    {
        Id = id;
        Title = title;
        Slug = slug;
        Summary = summary;
        Skill = skill;
        CategoryId = categoryId;
        CoverImageAssetId = coverImageAssetId;
        CoverImageAlt = coverImageAlt;
        ContentJson = contentJson;
        TocJson = tocJson;
        EstimatedReadMinutes = estimatedReadMinutes;
        Status = KnowledgeArticleStatus.Draft;
        CreatedByUserId = actorUserId;
        UpdatedByUserId = actorUserId;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public KnowledgeSkill Skill { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid? CoverImageAssetId { get; private set; }
    public string? CoverImageAlt { get; private set; }
    public string ContentJson { get; private set; } = string.Empty;
    public string TocJson { get; private set; } = "[]";
    public int EstimatedReadMinutes { get; private set; }
    public KnowledgeArticleStatus Status { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public int Version { get; private set; }

    public IReadOnlyCollection<KnowledgeArticleTag> ArticleTags => _articleTags.AsReadOnly();
    public IReadOnlyCollection<KnowledgeArticleMediaAsset> MediaAssets => _mediaAssets.AsReadOnly();

    public bool CanBeDeleted => Status != KnowledgeArticleStatus.Published;

    public static KnowledgeArticle Create(string title, string slug, string summary, KnowledgeSkill skill,
        Guid categoryId, Guid? coverImageAssetId, string? coverImageAlt, string contentJson, string tocJson,
        int estimatedReadMinutes, IEnumerable<Guid> tagIds, IEnumerable<Guid> embeddedMediaAssetIds,
        Guid actorUserId, DateTimeOffset now)
    {
        var normalizedTitle = ValidateTitle(title);
        var normalizedSlug = ValidateSlug(slug);
        var normalizedSummary = ValidateSummary(summary);
        ValidateSkill(skill);
        ArgumentOutOfRangeException.ThrowIfEqual(categoryId, Guid.Empty);
        ValidateOptionalAssetId(coverImageAssetId);
        var normalizedCoverAlt = NormalizeCoverImageAlt(coverImageAlt);
        var normalizedContentJson = ValidateJson(contentJson, nameof(contentJson));
        var normalizedTocJson = ValidateJson(tocJson, nameof(tocJson));
        var safeReadMinutes = Math.Max(1, estimatedReadMinutes);
        ArgumentOutOfRangeException.ThrowIfEqual(actorUserId, Guid.Empty);

        var article = new KnowledgeArticle(Guid.NewGuid(), normalizedTitle, normalizedSlug, normalizedSummary, skill,
            categoryId, coverImageAssetId, normalizedCoverAlt, normalizedContentJson, normalizedTocJson,
            safeReadMinutes, actorUserId, now);
        article.SyncTags(tagIds);
        article.SyncMediaAssets(embeddedMediaAssetIds);
        return article;
    }

    public void Update(string title, string slug, string summary, KnowledgeSkill skill, Guid categoryId,
        Guid? coverImageAssetId, string? coverImageAlt, string contentJson, string tocJson, int estimatedReadMinutes,
        IEnumerable<Guid> tagIds, IEnumerable<Guid> embeddedMediaAssetIds, Guid actorUserId, DateTimeOffset now)
    {
        Title = ValidateTitle(title);
        Slug = ValidateSlug(slug);
        Summary = ValidateSummary(summary);
        ValidateSkill(skill);
        ArgumentOutOfRangeException.ThrowIfEqual(categoryId, Guid.Empty);
        ValidateOptionalAssetId(coverImageAssetId);
        ArgumentOutOfRangeException.ThrowIfEqual(actorUserId, Guid.Empty);

        Skill = skill;
        CategoryId = categoryId;
        CoverImageAssetId = coverImageAssetId;
        CoverImageAlt = NormalizeCoverImageAlt(coverImageAlt);
        ContentJson = ValidateJson(contentJson, nameof(contentJson));
        TocJson = ValidateJson(tocJson, nameof(tocJson));
        EstimatedReadMinutes = Math.Max(1, estimatedReadMinutes);

        SyncTags(tagIds);
        SyncMediaAssets(embeddedMediaAssetIds);
        Touch(actorUserId, now);
    }

    public void Publish(Guid actorUserId, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(actorUserId, Guid.Empty);
        if (Status == KnowledgeArticleStatus.Published)
            throw new InvalidOperationException("Knowledge article is already published.");

        Status = KnowledgeArticleStatus.Published;
        PublishedAtUtc ??= now;
        Touch(actorUserId, now);
    }

    public void Unpublish(Guid actorUserId, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(actorUserId, Guid.Empty);
        if (Status != KnowledgeArticleStatus.Published)
            throw new InvalidOperationException("Only a published Knowledge article can be unpublished.");

        Status = KnowledgeArticleStatus.Draft;
        Touch(actorUserId, now);
    }

    public void Archive(Guid actorUserId, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(actorUserId, Guid.Empty);
        if (Status == KnowledgeArticleStatus.Archived)
            throw new InvalidOperationException("Knowledge article is already archived.");

        Status = KnowledgeArticleStatus.Archived;
        Touch(actorUserId, now);
    }

    private void SyncTags(IEnumerable<Guid> tagIds)
    {
        ArgumentNullException.ThrowIfNull(tagIds);
        var distinctTagIds = tagIds.Where(id => id != Guid.Empty).Distinct().ToHashSet();
        _articleTags.RemoveAll(link => !distinctTagIds.Contains(link.TagId));
        foreach (var tagId in distinctTagIds)
        {
            if (_articleTags.All(link => link.TagId != tagId))
                _articleTags.Add(KnowledgeArticleTag.Create(Id, tagId));
        }
    }

    private void SyncMediaAssets(IEnumerable<Guid> embeddedMediaAssetIds)
    {
        ArgumentNullException.ThrowIfNull(embeddedMediaAssetIds);
        var distinctAssetIds = embeddedMediaAssetIds.Where(id => id != Guid.Empty).ToHashSet();
        if (CoverImageAssetId is { } coverId && coverId != Guid.Empty)
            distinctAssetIds.Add(coverId);

        _mediaAssets.RemoveAll(link => !distinctAssetIds.Contains(link.MediaAssetId));
        foreach (var assetId in distinctAssetIds)
        {
            if (_mediaAssets.All(link => link.MediaAssetId != assetId))
                _mediaAssets.Add(KnowledgeArticleMediaAsset.Create(Id, assetId));
        }
    }

    private void Touch(Guid actorUserId, DateTimeOffset now)
    {
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
        Version++;
    }

    private static string ValidateTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var trimmed = title.Trim();
        if (trimmed.Length > 200)
            throw new ArgumentException("Article title must be at most 200 characters.", nameof(title));
        return trimmed;
    }

    private static string ValidateSlug(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var normalized = slug.Trim().ToLowerInvariant();
        if (normalized.Length > 220)
            throw new ArgumentException("Article slug must be at most 220 characters.", nameof(slug));
        return normalized;
    }

    private static string ValidateSummary(string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        var trimmed = summary.Trim();
        if (trimmed.Length > 500)
            throw new ArgumentException("Article summary must be at most 500 characters.", nameof(summary));
        return trimmed;
    }

    private static void ValidateSkill(KnowledgeSkill skill)
    {
        if (!Enum.IsDefined(skill))
            throw new ArgumentOutOfRangeException(nameof(skill));
    }

    private static void ValidateOptionalAssetId(Guid? assetId)
    {
        if (assetId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(assetId));
    }

    private static string? NormalizeCoverImageAlt(string? coverImageAlt)
    {
        if (string.IsNullOrWhiteSpace(coverImageAlt))
            return null;
        var trimmed = coverImageAlt.Trim();
        if (trimmed.Length > 300)
            throw new ArgumentException("Cover image alt text must be at most 300 characters.", nameof(coverImageAlt));
        return trimmed;
    }

    private static string ValidateJson(string json, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json, paramName);
        return json.Trim();
    }
}

public sealed class KnowledgeArticleTag
{
    private KnowledgeArticleTag()
    {
    }

    private KnowledgeArticleTag(Guid articleId, Guid tagId)
    {
        ArticleId = articleId;
        TagId = tagId;
    }

    public Guid ArticleId { get; private set; }
    public Guid TagId { get; private set; }

    internal static KnowledgeArticleTag Create(Guid articleId, Guid tagId) => new(articleId, tagId);
}

public sealed class KnowledgeArticleMediaAsset
{
    private KnowledgeArticleMediaAsset()
    {
    }

    private KnowledgeArticleMediaAsset(Guid articleId, Guid mediaAssetId)
    {
        ArticleId = articleId;
        MediaAssetId = mediaAssetId;
    }

    public Guid ArticleId { get; private set; }
    public Guid MediaAssetId { get; private set; }

    internal static KnowledgeArticleMediaAsset Create(Guid articleId, Guid mediaAssetId) => new(articleId, mediaAssetId);
}

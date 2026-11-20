namespace TOEICMaster.Domain.Entities;

public sealed class KnowledgeCategory
{
    private KnowledgeCategory()
    {
    }

    private KnowledgeCategory(Guid id, string name, string slug, string? description, int displayOrder,
        Guid actorUserId, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Description = description;
        DisplayOrder = displayOrder;
        CreatedByUserId = actorUserId;
        UpdatedByUserId = actorUserId;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public int Version { get; private set; }

    public static KnowledgeCategory Create(string name, string slug, string? description, int displayOrder,
        Guid actorUserId, DateTimeOffset now)
    {
        var normalizedName = ValidateName(name);
        var normalizedSlug = ValidateSlug(slug);
        var normalizedDescription = NormalizeDescription(description);
        ValidateDisplayOrder(displayOrder);
        ArgumentOutOfRangeException.ThrowIfEqual(actorUserId, Guid.Empty);

        return new KnowledgeCategory(Guid.NewGuid(), normalizedName, normalizedSlug, normalizedDescription,
            displayOrder, actorUserId, now);
    }

    public void Update(string name, string slug, string? description, int displayOrder, Guid actorUserId,
        DateTimeOffset now)
    {
        Name = ValidateName(name);
        Slug = ValidateSlug(slug);
        Description = NormalizeDescription(description);
        ValidateDisplayOrder(displayOrder);
        ArgumentOutOfRangeException.ThrowIfEqual(actorUserId, Guid.Empty);

        DisplayOrder = displayOrder;
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
        Version++;
    }

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (trimmed.Length > 100)
            throw new ArgumentException("Category name must be at most 100 characters.", nameof(name));
        return trimmed;
    }

    private static string ValidateSlug(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var normalized = slug.Trim().ToLowerInvariant();
        if (normalized.Length > 120)
            throw new ArgumentException("Category slug must be at most 120 characters.", nameof(slug));
        return normalized;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;
        var trimmed = description.Trim();
        if (trimmed.Length > 500)
            throw new ArgumentException("Category description must be at most 500 characters.", nameof(description));
        return trimmed;
    }

    private static void ValidateDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order must be non-negative.");
    }
}

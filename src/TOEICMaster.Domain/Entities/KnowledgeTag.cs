namespace TOEICMaster.Domain.Entities;

public sealed class KnowledgeTag
{
    private KnowledgeTag()
    {
    }

    private KnowledgeTag(Guid id, string name, string slug, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        Slug = slug;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static KnowledgeTag Create(string name, string slug, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var trimmedName = name.Trim();
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (trimmedName.Length > 50)
            throw new ArgumentException("Tag name must be at most 50 characters.", nameof(name));
        if (normalizedSlug.Length > 64)
            throw new ArgumentException("Tag slug must be at most 64 characters.", nameof(slug));

        return new KnowledgeTag(Guid.NewGuid(), trimmedName, normalizedSlug, createdAtUtc);
    }
}

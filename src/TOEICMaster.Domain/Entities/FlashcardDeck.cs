using System.Text;

namespace TOEICMaster.Domain.Entities;

public static class FlashcardMasteryStatus
{
    public const string New = "new";
    public const string Learning = "learning";
    public const string Review = "review";
    public const string Mastered = "mastered";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        New,
        Learning,
        Review,
        Mastered
    };

    public static string Normalize(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        var normalized = status.Trim().ToLowerInvariant();
        if (!Allowed.Contains(normalized))
            throw new ArgumentException($"Invalid flashcard mastery status '{status}'.", nameof(status));
        return normalized;
    }
}

public static class FlashcardCategories
{
    public const string Office = "office";
    public const string Contracts = "contracts";
    public const string Finance = "finance";
    public const string Travel = "travel";
    public const string Marketing = "marketing";
    public const string Hr = "hr";
    public const string Custom = "custom";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Office,
        Contracts,
        Finance,
        Travel,
        Marketing,
        Hr,
        Custom
    };
}

public sealed record FlashcardCardInput(
    string Term,
    string? Phonetic,
    string? PartOfSpeech,
    string MeaningVi,
    string? ExampleEn,
    string? ExampleVi,
    IReadOnlyList<string>? Collocations,
    string? ToeicPart,
    string? Level);

public sealed class FlashcardDeck
{
    private readonly List<FlashcardCard> _cards = [];

    private FlashcardDeck()
    {
    }

    private FlashcardDeck(
        Guid id,
        Guid? ownerUserId,
        string slug,
        string title,
        string description,
        string category,
        string targetScore,
        int estimatedMinutes,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Slug = slug;
        Title = title;
        Description = description;
        Category = category;
        TargetScore = targetScore;
        EstimatedMinutes = estimatedMinutes;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = FlashcardCategories.Custom;
    public string TargetScore { get; private set; } = string.Empty;
    public int EstimatedMinutes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsCustom => OwnerUserId.HasValue;
    public IReadOnlyCollection<FlashcardCard> Cards => _cards.AsReadOnly();

    public static FlashcardDeck CreateBuiltIn(
        Guid id,
        string slug,
        string title,
        string description,
        string category,
        string targetScore,
        int estimatedMinutes,
        IReadOnlyList<(Guid CardId, FlashcardCardInput Input)> cards,
        DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(cards);

        if (cards.Count == 0)
            throw new ArgumentException("A flashcard deck must contain at least one card.", nameof(cards));

        var normalizedCategory = category.Trim().ToLowerInvariant();
        if (!FlashcardCategories.Allowed.Contains(normalizedCategory))
            throw new ArgumentException($"Invalid category '{category}'.", nameof(category));

        var deck = new FlashcardDeck(
            id,
            ownerUserId: null,
            slug.Trim().ToLowerInvariant(),
            title.Trim(),
            description.Trim(),
            normalizedCategory,
            targetScore.Trim(),
            Math.Max(1, estimatedMinutes),
            nowUtc,
            nowUtc);

        for (var i = 0; i < cards.Count; i++)
        {
            deck._cards.Add(FlashcardCard.Create(cards[i].CardId, deck.Id, i + 1, cards[i].Input));
        }

        return deck;
    }

    public static FlashcardDeck CreateCustom(
        Guid ownerUserId,
        string title,
        string? description,
        IReadOnlyList<FlashcardCardInput> cards,
        DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(ownerUserId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(cards);

        if (cards.Count == 0)
            throw new ArgumentException("At least one card is required.", nameof(cards));

        var cleanTitle = title.Trim();
        var cleanDescription = string.IsNullOrWhiteSpace(description)
            ? "Your custom TOEIC vocabulary deck."
            : description.Trim();

        var deckId = Guid.NewGuid();
        var suffix = deckId.ToString("N")[..6];
        var slug = $"{Slugify(cleanTitle)}-{suffix}";
        var estimatedMinutes = Math.Max(2, (int)Math.Ceiling(cards.Count * 0.8));

        var deck = new FlashcardDeck(
            deckId,
            ownerUserId,
            slug,
            cleanTitle,
            cleanDescription,
            FlashcardCategories.Custom,
            "Personal",
            estimatedMinutes,
            nowUtc,
            nowUtc);

        for (var i = 0; i < cards.Count; i++)
        {
            deck._cards.Add(FlashcardCard.Create(Guid.NewGuid(), deck.Id, i + 1, cards[i]));
        }

        return deck;
    }

    public FlashcardCard AddCard(FlashcardCardInput input, DateTimeOffset nowUtc)
    {
        if (!IsCustom)
            throw new InvalidOperationException("Cannot add cards to a built-in flashcard deck.");

        ArgumentNullException.ThrowIfNull(input);

        var nextOrdinal = _cards.Count == 0 ? 1 : _cards.Max(card => card.Ordinal) + 1;
        var card = FlashcardCard.Create(Guid.NewGuid(), Id, nextOrdinal, input);
        _cards.Add(card);
        EstimatedMinutes = Math.Max(2, (int)Math.Ceiling(_cards.Count * 0.8));
        UpdatedAtUtc = nowUtc;
        return card;
    }

    public static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousDash = false;

        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var mapped = ch switch
            {
                '\u0111' or '\u0110' => 'd',
                _ => char.ToLowerInvariant(ch)
            };

            if ((mapped >= 'a' && mapped <= 'z') || (mapped >= '0' && mapped <= '9'))
            {
                builder.Append(mapped);
                previousDash = false;
            }
            else if (!previousDash && builder.Length > 0)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrEmpty(result) ? "custom-deck" : result;
    }
}

public sealed class FlashcardCard
{
    private static readonly IReadOnlySet<string> AllowedLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "B1",
        "B2",
        "C1"
    };

    private FlashcardCard()
    {
    }

    private FlashcardCard(
        Guid id,
        Guid deckId,
        int ordinal,
        string term,
        string phonetic,
        string partOfSpeech,
        string meaningVi,
        string exampleEn,
        string exampleVi,
        string collocationsSerialized,
        string toeicPart,
        string level)
    {
        Id = id;
        DeckId = deckId;
        Ordinal = ordinal;
        Term = term;
        Phonetic = phonetic;
        PartOfSpeech = partOfSpeech;
        MeaningVi = meaningVi;
        ExampleEn = exampleEn;
        ExampleVi = exampleVi;
        CollocationsSerialized = collocationsSerialized;
        ToeicPart = toeicPart;
        Level = level;
    }

    public Guid Id { get; private set; }
    public Guid DeckId { get; private set; }
    public int Ordinal { get; private set; }
    public string Term { get; private set; } = string.Empty;
    public string Phonetic { get; private set; } = string.Empty;
    public string PartOfSpeech { get; private set; } = "noun";
    public string MeaningVi { get; private set; } = string.Empty;
    public string ExampleEn { get; private set; } = string.Empty;
    public string ExampleVi { get; private set; } = string.Empty;
    public string CollocationsSerialized { get; private set; } = string.Empty;
    public string ToeicPart { get; private set; } = string.Empty;
    public string Level { get; private set; } = "B2";

    public IReadOnlyList<string> GetCollocations() =>
        string.IsNullOrWhiteSpace(CollocationsSerialized)
            ? []
            : CollocationsSerialized.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal static FlashcardCard Create(Guid id, Guid deckId, int ordinal, FlashcardCardInput input)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(deckId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Term);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.MeaningVi);

        var level = string.IsNullOrWhiteSpace(input.Level)
            ? "B2"
            : input.Level.Trim().ToUpperInvariant();
        if (!AllowedLevels.Contains(level))
            level = "B2";

        var collocations = input.Collocations is null
            ? string.Empty
            : string.Join('|', input.Collocations
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim().Replace("|", string.Empty)));

        return new FlashcardCard(
            id,
            deckId,
            ordinal,
            input.Term.Trim(),
            input.Phonetic?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(input.PartOfSpeech) ? "noun" : input.PartOfSpeech.Trim(),
            input.MeaningVi.Trim(),
            input.ExampleEn?.Trim() ?? string.Empty,
            input.ExampleVi?.Trim() ?? string.Empty,
            collocations,
            string.IsNullOrWhiteSpace(input.ToeicPart) ? "Personal vocabulary" : input.ToeicPart.Trim(),
            level);
    }
}

public sealed class UserFlashcardProgress
{
    private UserFlashcardProgress()
    {
    }

    private UserFlashcardProgress(
        Guid id,
        Guid userId,
        Guid cardId,
        string status,
        bool starred,
        int reviewCount,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        CardId = cardId;
        Status = status;
        Starred = starred;
        ReviewCount = reviewCount;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CardId { get; private set; }
    public string Status { get; private set; } = FlashcardMasteryStatus.New;
    public bool Starred { get; private set; }
    public int ReviewCount { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static UserFlashcardProgress Create(
        Guid userId,
        Guid cardId,
        string status,
        bool starred,
        DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(cardId, Guid.Empty);

        var normalizedStatus = FlashcardMasteryStatus.Normalize(status);
        var initialReviewCount = normalizedStatus == FlashcardMasteryStatus.New ? 0 : 1;

        return new UserFlashcardProgress(
            Guid.NewGuid(),
            userId,
            cardId,
            normalizedStatus,
            starred,
            initialReviewCount,
            nowUtc);
    }

    public void Update(string status, bool starred, DateTimeOffset nowUtc)
    {
        var normalizedStatus = FlashcardMasteryStatus.Normalize(status);
        if (normalizedStatus != FlashcardMasteryStatus.New &&
            (normalizedStatus != Status || starred == Starred))
        {
            ReviewCount += 1;
        }

        Status = normalizedStatus;
        Starred = starred;
        UpdatedAtUtc = nowUtc;
    }
}

public sealed class UserFlashcardMatchRecord
{
    private UserFlashcardMatchRecord()
    {
    }

    private UserFlashcardMatchRecord(
        Guid id,
        Guid userId,
        Guid deckId,
        int bestTimeMs,
        int bestCombo,
        int playCount,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        DeckId = deckId;
        BestTimeMs = bestTimeMs;
        BestCombo = bestCombo;
        PlayCount = playCount;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid DeckId { get; private set; }
    public int BestTimeMs { get; private set; }
    public int BestCombo { get; private set; }
    public int PlayCount { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static UserFlashcardMatchRecord Create(
        Guid userId,
        Guid deckId,
        int elapsedMs,
        int maxCombo,
        DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(deckId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elapsedMs);
        ArgumentOutOfRangeException.ThrowIfNegative(maxCombo);

        return new UserFlashcardMatchRecord(
            Guid.NewGuid(),
            userId,
            deckId,
            elapsedMs,
            maxCombo,
            playCount: 1,
            nowUtc);
    }

    public bool RecordRun(int elapsedMs, int maxCombo, DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elapsedMs);
        ArgumentOutOfRangeException.ThrowIfNegative(maxCombo);

        var isNewRecord = elapsedMs < BestTimeMs;
        if (isNewRecord)
            BestTimeMs = elapsedMs;

        if (maxCombo > BestCombo)
            BestCombo = maxCombo;

        PlayCount += 1;
        UpdatedAtUtc = nowUtc;
        return isNewRecord;
    }
}

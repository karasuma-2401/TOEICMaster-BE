namespace TOEICMaster.Application.Flashcards;

public sealed record FlashcardCardDto(
    Guid Id,
    string Term,
    string Phonetic,
    string PartOfSpeech,
    string MeaningVi,
    string ExampleEn,
    string ExampleVi,
    IReadOnlyList<string> Collocations,
    string ToeicPart,
    string Level);

public sealed record FlashcardDeckDto(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    string Category,
    string TargetScore,
    int EstimatedMinutes,
    bool IsCustom,
    IReadOnlyList<FlashcardCardDto> Cards);

public sealed record CardProgressDto(
    string Status,
    bool Starred,
    int ReviewCount,
    DateTimeOffset UpdatedAt);

public sealed record MatchRecordDto(
    int BestTimeMs,
    int BestCombo,
    int PlayCount,
    DateTimeOffset UpdatedAt,
    bool IsNewRecord = false);

public sealed record FlashcardWorkspaceDto(
    IReadOnlyList<FlashcardDeckDto> Decks,
    IReadOnlyDictionary<string, CardProgressDto> Progress,
    IReadOnlyDictionary<string, MatchRecordDto> MatchRecords);

public sealed record CreateFlashcardCardRequest(
    string Term,
    string? Phonetic,
    string? PartOfSpeech,
    string MeaningVi,
    string? ExampleEn,
    string? ExampleVi,
    IReadOnlyList<string>? Collocations,
    string? ToeicPart,
    string? Level);

public sealed record CreateFlashcardDeckRequest(
    string Title,
    string? Description,
    IReadOnlyList<CreateFlashcardCardRequest> Cards);

public sealed record UpsertFlashcardProgressRequest(
    string Status,
    bool Starred);

public sealed record RecordFlashcardMatchRequest(
    int ElapsedMs,
    int MaxCombo);

using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Exams;

public sealed record ExamDraftDto(Guid ExamId, Guid RevisionId, string Code, string Title, int VersionNumber,
    ExamRevisionStatus Status, int QuestionCount)
{
    public static ExamDraftDto From(Exam exam, ExamRevision revision) => new(exam.Id, revision.Id, exam.Code, exam.Title,
        revision.VersionNumber, revision.Status, revision.Blocks.Sum(block => block.Questions.Count));
}

public sealed record ScoreProfileDto(Guid Id, string Name, bool IsComplete, int MappingCount)
{
    public static ScoreProfileDto From(ScoreProfile profile) => new(profile.Id, profile.Name, profile.IsComplete,
        profile.Conversions.Count);
}

public sealed record ExamMediaAssetDto(Guid Id, string ContentType, string Kind, long ByteSize, int? DurationMs = null)
{
    public static ExamMediaAssetDto From(TOEICMaster.Domain.Entities.ExamMediaAsset asset) => new(asset.Id,
        asset.ContentType, asset.Kind.ToString(), asset.ByteSize, asset.DurationMs);
}

public sealed record ExamMediaUploadIntentDto(Guid IntentId, string UploadUrl, DateTimeOffset ExpiresAtUtc,
    IReadOnlyDictionary<string, string> RequiredHeaders)
{
    public static ExamMediaUploadIntentDto From(TOEICMaster.Domain.Entities.ExamMediaUploadIntent intent,
        string uploadUrl) => new(intent.Id, uploadUrl, intent.ExpiresAtUtc,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = intent.ContentType });
}

public sealed record ExamListItemDto(Guid ExamId, string Code, string Title, Guid RevisionId, int VersionNumber,
    ExamRevisionStatus Status, DateTimeOffset CreatedAtUtc);

public sealed record ExamPageDto(IReadOnlyList<ExamListItemDto> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)(((long)TotalCount + PageSize - 1) / PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}

public sealed record ExamRevisionDetailDto(Guid ExamId, string Code, string Title, Guid RevisionId, int VersionNumber,
    ExamRevisionStatus Status, Guid ScoreProfileId, IReadOnlyList<ExamBlockDto> Blocks)
{
    public static ExamRevisionDetailDto From(Exam exam, ExamRevision revision,
        IReadOnlyDictionary<Guid, int?>? mediaDurations = null) => new(exam.Id, exam.Code, exam.Title,
        revision.Id, revision.VersionNumber, revision.Status, revision.ScoreProfileId,
        revision.Blocks.OrderBy(block => block.FirstQuestionNumber)
            .Select(block => ExamBlockDto.From(block, mediaDurations)).ToArray());
}

public sealed record ExamBlockDto(string Key, int Part, ExamBlockType Type, int FirstQuestionNumber,
    Guid? AudioAssetId, IReadOnlyList<ExamContextBlockDto> ContextBlocks, IReadOnlyList<ExamQuestionDto> Questions,
    int? AudioDurationMs = null)
{
    public static ExamBlockDto From(ExamBlock block, IReadOnlyDictionary<Guid, int?>? mediaDurations = null)
    {
        int? audioDurationMs = block.AudioAssetId is { } assetId &&
            mediaDurations is not null &&
            mediaDurations.TryGetValue(assetId, out var durationMs)
            ? durationMs
            : null;
        return new(block.Key, block.Part, block.Type, block.FirstQuestionNumber, block.AudioAssetId,
            block.ContextBlocks.OrderBy(context => context.Ordinal).Select(ExamContextBlockDto.From).ToArray(),
            block.Questions.OrderBy(question => question.Number).Select(ExamQuestionDto.From).ToArray(),
            audioDurationMs);
    }
}

public sealed record ExamContextBlockDto(int Ordinal, ExamContextType Type, string? Text, Guid? ImageAssetId,
    string? AltText, bool ImageRequired)
{
    public static ExamContextBlockDto From(ExamContextBlock context) => new(context.Ordinal, context.Type, context.Text,
        context.ImageAssetId, context.AltText, context.ImageRequired);
}

public sealed record ExamQuestionDto(int Number, string? Code, string? Stem, string? OptionA, string? OptionB,
    string? OptionC, string? OptionD, string? CorrectKey, string? Translation, string? Explanation, Guid? ImageAssetId,
    bool ImageRequired)
{
    public static ExamQuestionDto From(ExamQuestion question) => new(question.Number, question.Code, question.Stem,
        question.OptionA, question.OptionB, question.OptionC, question.OptionD, question.CorrectKey,
        question.Translation, question.Explanation, question.ImageAssetId, question.ImageRequired);
}

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

public sealed record ExamMediaAssetDto(Guid Id, string ContentType)
{
    public static ExamMediaAssetDto From(TOEICMaster.Domain.Entities.ExamMediaAsset asset) => new(asset.Id,
        asset.ContentType);
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
    ExamRevisionStatus Status, Guid ScoreProfileId, bool HasMasterAudio, IReadOnlyList<ExamBlockDto> Blocks)
{
    public static ExamRevisionDetailDto From(Exam exam, ExamRevision revision) => new(exam.Id, exam.Code, exam.Title,
        revision.Id, revision.VersionNumber, revision.Status, revision.ScoreProfileId,
        !string.IsNullOrWhiteSpace(revision.MasterAudioObjectKey), revision.Blocks.OrderBy(block => block.FirstQuestionNumber)
            .Select(ExamBlockDto.From).ToArray());
}

public sealed record ExamBlockDto(string Key, int Part, ExamBlockType Type, int FirstQuestionNumber,
    IReadOnlyList<ExamContextBlockDto> ContextBlocks, IReadOnlyList<ExamQuestionDto> Questions)
{
    public static ExamBlockDto From(ExamBlock block) => new(block.Key, block.Part, block.Type, block.FirstQuestionNumber,
        block.ContextBlocks.OrderBy(context => context.Ordinal).Select(ExamContextBlockDto.From).ToArray(),
        block.Questions.OrderBy(question => question.Number).Select(ExamQuestionDto.From).ToArray());
}

public sealed record ExamContextBlockDto(int Ordinal, ExamContextType Type, string? Text, Guid? ImageAssetId,
    string? AltText)
{
    public static ExamContextBlockDto From(ExamContextBlock context) => new(context.Ordinal, context.Type, context.Text,
        context.ImageAssetId, context.AltText);
}

public sealed record ExamQuestionDto(int Number, string? Code, string? Stem, string? OptionA, string? OptionB,
    string? OptionC, string? OptionD, string? CorrectKey, string? Translation, string? Explanation, Guid? ImageAssetId)
{
    public static ExamQuestionDto From(ExamQuestion question) => new(question.Number, question.Code, question.Stem,
        question.OptionA, question.OptionB, question.OptionC, question.OptionD, question.CorrectKey,
        question.Translation, question.Explanation, question.ImageAssetId);
}

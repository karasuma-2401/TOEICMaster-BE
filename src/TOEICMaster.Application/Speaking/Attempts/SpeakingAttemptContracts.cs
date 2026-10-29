using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using TOEICMaster.Application.Speaking.Admin;
using TOEICMaster.Application.Speaking.Evaluations;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Attempts;

/// <summary>A signed-in user, or an anonymous browser session stored only as a SHA-256 hash.</summary>
public sealed record SpeakingAttemptOwner(Guid? UserId, string? AnonymousSessionHash)
{
    public static SpeakingAttemptOwner ForUser(Guid userId) => new(userId, null);

    public static SpeakingAttemptOwner ForAnonymousSession(string anonymousSessionId) =>
        new(null, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(anonymousSessionId))));
}

public sealed class SpeakingAttemptOwnerValidator : AbstractValidator<SpeakingAttemptOwner>
{
    public SpeakingAttemptOwnerValidator() => RuleFor(owner => owner).Must(owner =>
            (owner.UserId is not null) != !string.IsNullOrWhiteSpace(owner.AnonymousSessionHash))
        .OverridePropertyName("Owner").WithMessage("Lượt làm cần đúng một chủ sở hữu.");
}

/// <summary>Starts from a published test (with a mode) or practises one published exercise on its own.</summary>
public sealed record StartSpeakingAttemptInput(Guid? TestId, string? Mode, Guid? ExerciseId = null);

public sealed record SpeakingPartSummaryDto(int Part, int QuestionCount, int DurationSeconds);

/// <summary>Published test for learners; never contains rubric notes or model answers.</summary>
public sealed record SpeakingPublicTestDto(Guid Id, string Code, string Title, string Description,
    DateTimeOffset? PublishedAtUtc, IReadOnlyList<SpeakingPartSummaryDto> Parts);

/// <summary>Published bank exercise for Part practice; never contains rubric notes or model answers.</summary>
public sealed record SpeakingPublicExerciseDto(Guid Id, string Code, int Part, string Topic, string Difficulty,
    int QuestionCount, int DurationSeconds, DateTimeOffset UpdatedAtUtc);

public sealed record SpeakingRecordingDto(int DurationSeconds, DateTimeOffset UploadedAtUtc);

public sealed record SpeakingAttemptQuestionDto(int Position, int Part, int QuestionIndex, int ReadingSeconds,
    int PreparationSeconds, int ResponseSeconds, DateTimeOffset? LockedAtUtc, SpeakingRecordingDto? Recording,
    SpeakingContentDto Content);

public sealed record SpeakingReviewQuestionDto(int Position, string RubricNotes, string ModelAnswer);

/// <summary>
/// <see cref="TestId"/> is set for test attempts and <see cref="ExerciseId"/> for Part practice; TestCode and
/// TestTitle then describe the exercise.
/// </summary>
public sealed record SpeakingAttemptDto(Guid Id, Guid? TestId, Guid? ExerciseId, string TestCode, string TestTitle, string Mode,
    string Status, int? CurrentQuestionPosition, string? CurrentPhase, DateTimeOffset? PhaseEndsAtUtc,
    DateTimeOffset ServerNowUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset? SubmittedAtUtc,
    IReadOnlyList<SpeakingAttemptQuestionDto> Questions, IReadOnlyList<SpeakingReviewQuestionDto> ReviewQuestions,
    SpeakingEvaluationDto? Evaluation = null);

public sealed record SpeakingAttemptSummaryDto(Guid Id, Guid? TestId, Guid? ExerciseId, string TestCode, string TestTitle, string Mode,
    string Status, int QuestionCount, int RecordedCount, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? SubmittedAtUtc, string? EvaluationStatus = null, int? EstimatedScore = null);

public sealed record SpeakingReviewSnapshot(string RubricNotes, string ModelAnswer);

public sealed record SpeakingTestHeader(string Code, string Title);

public interface ISpeakingAttemptRepository
{
    Task<SpeakingPageDto<SpeakingTestWithSlots>> GetPublishedTestPageAsync(string? query, int pageNumber,
        int pageSize, CancellationToken cancellationToken);
    Task<SpeakingTestWithSlots?> FindPublishedTestAsync(Guid testId, CancellationToken cancellationToken);
    /// <summary>An image asset referenced by the frozen snapshot of a published test.</summary>
    Task<SpeakingMediaAsset?> FindPublishedImageAsync(Guid assetId, CancellationToken cancellationToken);
    Task<SpeakingAttempt?> FindAttemptForUpdateAsync(Guid attemptId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SpeakingAttempt>> GetAttemptsAsync(SpeakingAttemptOwner owner, int take,
        CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, SpeakingTestHeader>> GetTestHeadersAsync(IReadOnlyCollection<Guid> testIds,
        CancellationToken cancellationToken);
    Task<SpeakingPageDto<SpeakingExercise>> GetPublishedExercisePageAsync(int? part, int pageNumber, int pageSize,
        CancellationToken cancellationToken);
    Task<SpeakingExercise?> FindPublishedExerciseAsync(Guid exerciseId, CancellationToken cancellationToken);
    /// <summary>Code and display title of exercises practised on their own.</summary>
    Task<IReadOnlyDictionary<Guid, SpeakingTestHeader>> GetExerciseHeadersAsync(IReadOnlyCollection<Guid> exerciseIds,
        CancellationToken cancellationToken);
    void AddAttempt(SpeakingAttempt attempt);
    Task<SpeakingMediaAsset?> FindMediaAssetAsync(Guid assetId, CancellationToken cancellationToken);
    void AddMediaAsset(SpeakingMediaAsset asset);
    void RemoveMediaAsset(SpeakingMediaAsset asset);
    /// <summary>Saves changes; a stale attempt version raises <see cref="SpeakingConcurrencyException"/>.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
    /// <summary>Forgets tracked entities so the attempt can be reloaded after a concurrency conflict.</summary>
    void ClearTracking();
}

public static class SpeakingAttemptMapping
{
    public static SpeakingPublicTestDto ToPublicDto(this SpeakingTestWithSlots source)
    {
        var parts = source.Slots.GroupBy(slot => slot.Part).OrderBy(group => group.Key)
            .Select(group => new SpeakingPartSummaryDto(group.Key,
                group.Count() * SpeakingTiming.QuestionsPerExercise(group.Key),
                SpeakingTiming.PartDurationSeconds(group.Key, group.Count()))).ToArray();
        var test = source.Test;
        return new SpeakingPublicTestDto(test.Id, test.Code, test.Title, test.Description, test.PublishedAtUtc, parts);
    }

    public static SpeakingAttemptDto ToDto(this SpeakingAttempt attempt, SpeakingTestHeader header,
        DateTimeOffset now, SpeakingEvaluation? evaluation = null)
    {
        var inProgress = attempt.Status == SpeakingAttemptStatus.InProgress;
        return new SpeakingAttemptDto(attempt.Id, attempt.TestId, attempt.ExerciseId, header.Code, header.Title, attempt.Mode.ToCode(),
            attempt.Status.ToCode(), inProgress ? attempt.CurrentPosition : null,
            inProgress ? attempt.CurrentPhase?.ToCode() : null, inProgress ? attempt.PhaseEndsAtUtc : null, now,
            attempt.CreatedAtUtc, attempt.SubmittedAtUtc,
            attempt.Questions.Select(question => question.ToDto(attempt)).ToArray(),
            inProgress
                ? []
                : attempt.Questions.Select(question =>
                {
                    var review = SpeakingJson.Deserialize<SpeakingReviewSnapshot>(question.ReviewJson);
                    return new SpeakingReviewQuestionDto(question.Position, review.RubricNotes, review.ModelAnswer);
                }).ToArray(), evaluation?.ToDto());
    }

    public static SpeakingAttemptSummaryDto ToSummaryDto(this SpeakingAttempt attempt, SpeakingTestHeader header,
        SpeakingEvaluation? evaluation = null) =>
        new(attempt.Id, attempt.TestId, attempt.ExerciseId, header.Code, header.Title, attempt.Mode.ToCode(), attempt.Status.ToCode(),
            attempt.Questions.Count, attempt.Questions.Count(question => question.RecordingAssetId is not null),
            attempt.CreatedAtUtc, attempt.UpdatedAtUtc, attempt.SubmittedAtUtc, evaluation?.Status.ToCode(),
            evaluation?.EstimatedScore);

    private static SpeakingAttemptQuestionDto ToDto(this SpeakingAttemptQuestion question, SpeakingAttempt attempt) =>
        new(question.Position, question.Part, question.QuestionIndex, question.ReadingSeconds,
            question.PreparationSeconds, question.ResponseSeconds, question.LockedAtUtc,
            question.RecordingAssetId is not null && question.RecordingUploadedAtUtc is { } uploadedAt
                ? new SpeakingRecordingDto(question.RecordingDurationSeconds ?? 0, uploadedAt)
                : null,
            VisibleContent(question, attempt));

    /// <summary>
    /// Hides Part 3/4 questions the learner has not reached: a question is visible once it is in the past, or it is
    /// the current question past its reading phase, or the attempt is submitted.
    /// </summary>
    internal static SpeakingContentDto VisibleContent(SpeakingAttemptQuestion question, SpeakingAttempt attempt)
    {
        var content = SpeakingJson.Deserialize<SpeakingContentDto>(question.ContentJson);
        if (content.Questions is null || attempt.Status != SpeakingAttemptStatus.InProgress)
            return content;
        var firstPosition = question.Position - question.QuestionIndex;
        var visible = content.Questions.Select((text, index) =>
        {
            var position = firstPosition + index;
            var reached = position < attempt.CurrentPosition ||
                          position == attempt.CurrentPosition && attempt.CurrentPhase != SpeakingPhase.Reading;
            return reached ? text : null;
        }).ToArray();
        return content with { Questions = visible };
    }

    /// <summary>Expands frozen test slots into attempt questions; Part 3/4 exercises become three questions.</summary>
    public static IReadOnlyList<SpeakingAttemptQuestion> BuildQuestions(Guid attemptId,
        IEnumerable<SpeakingTestExercise> slots)
    {
        var questions = new List<SpeakingAttemptQuestion>();
        foreach (var slot in slots.OrderBy(slot => slot.Part).ThenBy(slot => slot.SequenceNumber))
            AddQuestions(questions, attemptId, slot.Part, slot.ExerciseId,
                SpeakingJson.Deserialize<SpeakingExerciseDto>(slot.SnapshotJson!));
        return questions;
    }

    /// <summary>Snapshots one published exercise into the questions of a Part practice attempt.</summary>
    public static IReadOnlyList<SpeakingAttemptQuestion> BuildQuestions(Guid attemptId, SpeakingExercise exercise)
    {
        var questions = new List<SpeakingAttemptQuestion>();
        AddQuestions(questions, attemptId, exercise.Part, exercise.Id, exercise.ToDto());
        return questions;
    }

    public static SpeakingPublicExerciseDto ToPublicDto(this SpeakingExercise exercise) => new(exercise.Id,
        exercise.Code, exercise.Part, exercise.Topic, exercise.Difficulty.ToCode(),
        SpeakingTiming.QuestionsPerExercise(exercise.Part), SpeakingTiming.PartDurationSeconds(exercise.Part, 1),
        exercise.UpdatedAtUtc);

    public static SpeakingTestHeader ToHeader(this SpeakingExercise exercise) =>
        new(exercise.Code, $"Luyện Part {exercise.Part} · {exercise.Topic}");

    private static void AddQuestions(List<SpeakingAttemptQuestion> questions, Guid attemptId, int part,
        Guid exerciseId, SpeakingExerciseDto exercise)
    {
        var contentJson = SpeakingJson.Serialize(exercise.ToContent() with { Part = part });
        var reviewJson = SpeakingJson.Serialize(new SpeakingReviewSnapshot(exercise.RubricNotes,
            exercise.ModelAnswer));
        for (var index = 0; index < SpeakingTiming.QuestionsPerExercise(part); index++)
            questions.Add(SpeakingAttemptQuestion.Create(attemptId, questions.Count + 1, part, index, exerciseId,
                contentJson, reviewJson));
    }
}

using TOEICMaster.Application.ExamAttempts;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.TestDoubles;

/// <summary>
/// In-memory exam attempt repository over a real <see cref="Exam"/> aggregate. The exam has a published revision
/// with a few filled questions: Q1 (Part 1, key A), Q7 (Part 2, A-C only, key B) and Q101 (Part 5, key C),
/// plus sample audio and image assets with persisted durations.
/// </summary>
public sealed class InMemoryExamAttempts : IExamAttemptRepository
{
    public const int PartOneQuestion = 1;
    public const int PartTwoQuestion = 7;
    public const int PartFiveQuestion = 101;

    private readonly List<ExamAttempt> _attempts = [];
    private readonly Dictionary<Guid, ExamMediaAsset> _mediaAssets = [];

    public InMemoryExamAttempts()
    {
        var now = DateTimeOffset.UtcNow;
        ScoreProfile = ScoreProfile.Create("Test profile", now);
        foreach (var skill in new[] { ExamSkill.Listening, ExamSkill.Reading })
            for (var raw = 0; raw <= 100; raw++)
                ScoreProfile.SetScore(skill, raw, Math.Min(495, 5 + (raw * 5)));

        PartOneAudioAsset = ExamMediaAsset.Create("exam-media/p1-q1.mp3", "audio/mpeg", now, ExamMediaKind.Audio,
            2048, 18_500);
        PartTwoAudioAsset = ExamMediaAsset.Create("exam-media/p2-q7.mp3", "audio/mpeg", now, ExamMediaKind.Audio,
            1024, 12_000);
        PartOneImageAsset = ExamMediaAsset.Create("exam-media/q1.png", "image/png", now, ExamMediaKind.Image,
            4096);
        _mediaAssets[PartOneAudioAsset.Id] = PartOneAudioAsset;
        _mediaAssets[PartTwoAudioAsset.Id] = PartTwoAudioAsset;
        _mediaAssets[PartOneImageAsset.Id] = PartOneImageAsset;

        Exam = Exam.Create("LR-TEST-001", "Test exam", ScoreProfile.Id, now);
        var draft = Exam.Revisions.Single();
        draft.UpdateQuestion(PartOneQuestion, Content("P1-1", "A", withOptionD: true, PartOneImageAsset.Id));
        draft.UpdateQuestion(PartTwoQuestion, Content("P2-7", "B", withOptionD: false));
        draft.UpdateQuestion(PartFiveQuestion, Content("P5-101", "C", withOptionD: true));
        draft.AttachAudioAsset("P1-Q1", PartOneAudioAsset.Id);
        draft.AttachAudioAsset("P2-Q7", PartTwoAudioAsset.Id);
        Exam.PublishDraft(draft.Id);
        Revision = Exam.Revisions.Single();
    }

    public Exam Exam { get; }
    public ExamRevision Revision { get; }
    public ScoreProfile ScoreProfile { get; }
    public ExamMediaAsset PartOneAudioAsset { get; }
    public ExamMediaAsset PartTwoAudioAsset { get; }
    public ExamMediaAsset PartOneImageAsset { get; }
    public IReadOnlyList<ExamAttempt> Attempts => _attempts;

    public Guid QuestionId(int number) =>
        Revision.Blocks.SelectMany(block => block.Questions).Single(question => question.Number == number).Id;

    private static ExamQuestionContent Content(string code, string correctKey, bool withOptionD,
        Guid? imageAssetId = null) =>
        new(code, "Stem", "Option A", "Option B", "Option C", withOptionD ? "Option D" : null, correctKey,
            "Translation", "Explanation", imageAssetId);

    public Task<IReadOnlyList<PublishedLrExamDto>> GetPublishedExamsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PublishedLrExamDto> items =
        [
            new PublishedLrExamDto(
                Exam.Id,
                Exam.Code,
                Exam.Title,
                Revision.VersionNumber,
                200,
                100,
                100,
                Revision.CreatedAtUtc)
        ];
        return Task.FromResult(items);
    }

    public Task<ExamHeader?> GetExamHeaderAsync(Guid examId, CancellationToken cancellationToken) =>
        Task.FromResult<ExamHeader?>(examId == Exam.Id ? new ExamHeader(Exam.Id, Exam.Code, Exam.Title) : null);

    public Task<ExamRevision?> FindPublishedRevisionForExamAsync(Guid examId, CancellationToken cancellationToken) =>
        Task.FromResult(examId == Exam.Id ? Revision : null);

    public Task<ExamRevision?> FindRevisionAsync(Guid revisionId, CancellationToken cancellationToken) =>
        Task.FromResult(revisionId == Revision.Id ? Revision : null);

    public Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken) =>
        Task.FromResult(scoreProfileId == ScoreProfile.Id ? ScoreProfile : null);

    public Task<IReadOnlyDictionary<Guid, ExamQuestionMetadata>> GetQuestionMetadataAsync(
        IReadOnlyCollection<Guid> questionIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, ExamQuestionMetadata> result = Revision.Blocks
            .SelectMany(block => block.Questions.Select(question => new { block, question }))
            .Where(x => questionIds.Contains(x.question.Id))
            .ToDictionary(
                x => x.question.Id,
                x => new ExamQuestionMetadata(Revision.Id, x.question.Number, x.block.Part, x.question.OptionD is not null));
        return Task.FromResult(result);
    }

    public Task<ExamAttempt?> FindAttemptForUpdateAsync(Guid attemptId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_attempts.SingleOrDefault(a => a.Id == attemptId && a.UserId == userId));

    public Task<ExamAttempt?> GetAttemptAsync(Guid attemptId, Guid userId, CancellationToken cancellationToken) =>
        FindAttemptForUpdateAsync(attemptId, userId, cancellationToken);

    public Task<AttemptMediaLookupResult> GetAttemptMediaAccessAsync(Guid attemptId, Guid userId, Guid assetId,
        ExamMediaKind kind, CancellationToken cancellationToken)
    {
        var attempt = _attempts.SingleOrDefault(a => a.Id == attemptId && a.UserId == userId);
        if (attempt is null || attempt.RevisionId != Revision.Id)
            return Task.FromResult(new AttemptMediaLookupResult(AttemptMediaLookupStatus.NotFound, null));

        var referencedInRevision = kind == ExamMediaKind.Audio
            ? Revision.Blocks.Any(block => block.AudioAssetId == assetId)
            : Revision.Blocks.Any(block =>
                block.Questions.Any(question => question.ImageAssetId == assetId) ||
                block.ContextBlocks.Any(context => context.ImageAssetId == assetId));

        if (!referencedInRevision || !_mediaAssets.TryGetValue(assetId, out var asset) || asset.Kind != kind)
            return Task.FromResult(new AttemptMediaLookupResult(AttemptMediaLookupStatus.NotFound, null));

        if (kind == ExamMediaKind.Audio && attempt.Status == ExamAttemptStatus.InProgress && attempt.IsListeningLocked)
            return Task.FromResult(new AttemptMediaLookupResult(AttemptMediaLookupStatus.ListeningLocked, null));

        return Task.FromResult(new AttemptMediaLookupResult(AttemptMediaLookupStatus.Found, asset.ObjectKey));
    }

    public Task<IReadOnlyDictionary<Guid, int?>> GetMediaDurationsAsync(IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, int?> result = _mediaAssets.Values
            .Where(asset => assetIds.Contains(asset.Id))
            .ToDictionary(asset => asset.Id, asset => asset.DurationMs);
        return Task.FromResult(result);
    }

    public Task<ExamAttemptListeningPlaylistSource?> GetListeningPlaylistSourceAsync(Guid attemptId, Guid userId,
        CancellationToken cancellationToken)
    {
        var attempt = _attempts.SingleOrDefault(a => a.Id == attemptId && a.UserId == userId);
        if (attempt is null || attempt.RevisionId != Revision.Id)
            return Task.FromResult<ExamAttemptListeningPlaylistSource?>(null);

        var blocks = Revision.Blocks
            .Where(block => ExamAttempt.IsListeningPart(block.Part) && block.AudioAssetId is not null)
            .OrderBy(block => block.FirstQuestionNumber)
            .Select(block =>
            {
                var asset = _mediaAssets[block.AudioAssetId!.Value];
                return new ExamAttemptListeningBlockSource(block.Id, block.Part, block.FirstQuestionNumber, asset.Id,
                    asset.ObjectKey, asset.DurationMs);
            })
            .ToArray();

        return Task.FromResult<ExamAttemptListeningPlaylistSource?>(
            new ExamAttemptListeningPlaylistSource(attempt.Status, attempt.IsListeningLocked, blocks));
    }

    public Task<Guid?> FindActiveAttemptIdAsync(Guid userId, Guid examId, CancellationToken cancellationToken) =>
        Task.FromResult(_attempts
            .Where(a => a.UserId == userId && a.Status == ExamAttemptStatus.InProgress)
            .OrderByDescending(a => a.StartedAtUtc)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefault());

    public Task<(IReadOnlyList<ExamAttemptHistoryItemDto> Items, int TotalCount)> GetAttemptHistoryAsync(
        Guid userId,
        int pageIndex,
        int pageSize,
        Guid? examId,
        CancellationToken cancellationToken)
    {
        var filtered = _attempts
            .Where(a => a.UserId == userId && (examId is null || Exam.Id == examId.Value))
            .OrderByDescending(a => a.StartedAtUtc)
            .ToArray();
        var items = filtered
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ExamAttemptHistoryItemDto(
                a.Id,
                Exam.Id,
                Exam.Title,
                Exam.Code,
                a.Status,
                a.ListeningScaledScore,
                a.ReadingScaledScore,
                a.TotalScaledScore,
                a.IsTimeExpired,
                a.StartedAtUtc,
                a.SubmittedAtUtc,
                a.SubmittedAtUtc.HasValue ? (int)(a.SubmittedAtUtc.Value - a.StartedAtUtc).TotalSeconds : null))
            .ToArray();
        return Task.FromResult<(IReadOnlyList<ExamAttemptHistoryItemDto>, int)>((items, filtered.Length));
    }

    public void AddAttempt(ExamAttempt attempt) => _attempts.Add(attempt);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

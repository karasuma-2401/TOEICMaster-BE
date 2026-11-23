using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.ExamAttempts;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.ExamAttempts;

public sealed class EfExamAttemptRepository(ToeicDbContext db) : IExamAttemptRepository
{
    public async Task<IReadOnlyList<PublishedLrExamDto>> GetPublishedExamsAsync(CancellationToken cancellationToken)
    {
        var revisions = await db.ExamRevisions
            .AsNoTracking()
            .Where(revision => revision.Status == ExamRevisionStatus.Published)
            .Select(revision => new { revision.Id, revision.ExamId, revision.VersionNumber, revision.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var latest = revisions
            .GroupBy(revision => revision.ExamId)
            .Select(group => group.OrderByDescending(revision => revision.VersionNumber).First())
            .OrderByDescending(revision => revision.CreatedAtUtc)
            .ToList();
        if (latest.Count == 0) return [];

        var examIds = latest.Select(revision => revision.ExamId).ToList();
        var revisionIds = latest.Select(revision => revision.Id).ToList();
        var exams = await db.Exams.AsNoTracking()
            .Where(exam => examIds.Contains(exam.Id))
            .ToDictionaryAsync(exam => exam.Id, cancellationToken);
        var blockCounts = await db.ExamBlocks.AsNoTracking()
            .Where(block => revisionIds.Contains(block.RevisionId))
            .Select(block => new { block.RevisionId, block.Part, QuestionCount = block.Questions.Count })
            .ToListAsync(cancellationToken);
        var countsByRevision = blockCounts.GroupBy(block => block.RevisionId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return latest.Select(revision =>
        {
            var blocks = countsByRevision.TryGetValue(revision.Id, out var found) ? found : [];
            var total = blocks.Sum(block => block.QuestionCount);
            var listening = blocks.Where(block => block.Part is >= 1 and <= 4).Sum(block => block.QuestionCount);
            var exam = exams[revision.ExamId];
            return new PublishedLrExamDto(
                exam.Id, exam.Code, exam.Title, revision.VersionNumber, total, listening, total - listening, revision.CreatedAtUtc);
        }).ToList();
    }
    public async Task<ExamHeader?> GetExamHeaderAsync(Guid examId, CancellationToken cancellationToken)
    {
        return await db.Exams
            .AsNoTracking()
            .Where(e => e.Id == examId)
            .Select(e => new ExamHeader(e.Id, e.Code, e.Title))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ExamRevision?> FindPublishedRevisionForExamAsync(Guid examId, CancellationToken cancellationToken)
    {
        return await db.ExamRevisions
            .AsNoTracking()
            .Include(r => r.Blocks)
                .ThenInclude(b => b.Questions)
            .Include(r => r.Blocks)
                .ThenInclude(b => b.ContextBlocks)
            .AsSplitQuery()
            .Where(r => r.ExamId == examId && r.Status == ExamRevisionStatus.Published)
            .OrderByDescending(r => r.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ExamRevision?> FindRevisionAsync(Guid revisionId, CancellationToken cancellationToken)
    {
        return await db.ExamRevisions
            .AsNoTracking()
            .Include(r => r.Blocks)
                .ThenInclude(b => b.Questions)
            .Include(r => r.Blocks)
                .ThenInclude(b => b.ContextBlocks)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == revisionId, cancellationToken);
    }

    public async Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken)
    {
        return await db.ScoreProfiles
            .AsNoTracking()
            .Include(s => s.Conversions)
            .FirstOrDefaultAsync(s => s.Id == scoreProfileId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ExamQuestionMetadata>> GetQuestionMetadataAsync(
        IReadOnlyCollection<Guid> questionIds,
        CancellationToken cancellationToken)
    {
        var list = await (
            from q in db.ExamQuestions
            join b in db.ExamBlocks on q.BlockId equals b.Id
            where questionIds.Contains(q.Id)
            select new { q.Id, b.RevisionId, q.Number, b.Part, HasOptionD = q.OptionD != null }
        ).ToListAsync(cancellationToken);

        return list.ToDictionary(
            x => x.Id,
            x => new ExamQuestionMetadata(x.RevisionId, x.Number, x.Part, x.HasOptionD));
    }

    public async Task<ExamAttempt?> FindAttemptForUpdateAsync(Guid attemptId, Guid userId, CancellationToken cancellationToken)
    {
        return await db.ExamAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, cancellationToken);
    }

    public async Task<ExamAttempt?> GetAttemptAsync(Guid attemptId, Guid userId, CancellationToken cancellationToken)
    {
        return await db.ExamAttempts
            .Include(a => a.Answers)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, cancellationToken);
    }

    public async Task<AttemptMediaLookupResult> GetAttemptMediaAccessAsync(
        Guid attemptId,
        Guid userId,
        Guid assetId,
        ExamMediaKind kind,
        CancellationToken cancellationToken)
    {
        var attempt = await db.ExamAttempts.AsNoTracking()
            .Where(item => item.Id == attemptId && item.UserId == userId)
            .Select(item => new { item.RevisionId, item.Status, item.IsListeningLocked })
            .FirstOrDefaultAsync(cancellationToken);
        if (attempt is null || attempt.Status is not (ExamAttemptStatus.InProgress or ExamAttemptStatus.Completed))
            return new AttemptMediaLookupResult(AttemptMediaLookupStatus.NotFound, null);

        var isAttached = kind == ExamMediaKind.Audio
            ? await db.ExamBlocks.AsNoTracking()
                .AnyAsync(block => block.RevisionId == attempt.RevisionId && block.AudioAssetId == assetId,
                    cancellationToken)
            : await db.ExamBlocks.AsNoTracking()
                .AnyAsync(block => block.RevisionId == attempt.RevisionId &&
                    (block.Questions.Any(question => question.ImageAssetId == assetId) ||
                     block.ContextBlocks.Any(context => context.ImageAssetId == assetId)), cancellationToken);
        if (!isAttached)
            return new AttemptMediaLookupResult(AttemptMediaLookupStatus.NotFound, null);

        var objectKey = await db.ExamMediaAssets.AsNoTracking()
            .Where(asset => asset.Id == assetId && asset.Kind == kind)
            .Select(asset => asset.ObjectKey)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(objectKey))
            return new AttemptMediaLookupResult(AttemptMediaLookupStatus.NotFound, null);

        if (kind == ExamMediaKind.Audio && attempt.Status == ExamAttemptStatus.InProgress && attempt.IsListeningLocked)
            return new AttemptMediaLookupResult(AttemptMediaLookupStatus.ListeningLocked, null);

        return new AttemptMediaLookupResult(AttemptMediaLookupStatus.Found, objectKey);
    }

    public async Task<IReadOnlyDictionary<Guid, int?>> GetMediaDurationsAsync(
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0)
            return new Dictionary<Guid, int?>();
        var distinctAssetIds = assetIds.Distinct().ToArray();
        return await db.ExamMediaAssets.AsNoTracking()
            .Where(asset => distinctAssetIds.Contains(asset.Id))
            .Select(asset => new { asset.Id, asset.DurationMs })
            .ToDictionaryAsync(asset => asset.Id, asset => asset.DurationMs, cancellationToken);
    }

    public async Task<ExamAttemptListeningPlaylistSource?> GetListeningPlaylistSourceAsync(
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var attempt = await db.ExamAttempts.AsNoTracking()
            .Where(item => item.Id == attemptId && item.UserId == userId)
            .Select(item => new { item.RevisionId, item.Status, item.IsListeningLocked })
            .FirstOrDefaultAsync(cancellationToken);
        if (attempt is null)
            return null;

        if (attempt.Status != ExamAttemptStatus.InProgress || attempt.IsListeningLocked)
            return new ExamAttemptListeningPlaylistSource(attempt.Status, attempt.IsListeningLocked, []);

        var blocks = await (
            from block in db.ExamBlocks.AsNoTracking()
            join asset in db.ExamMediaAssets.AsNoTracking() on block.AudioAssetId equals asset.Id
            where block.RevisionId == attempt.RevisionId
                && block.Part >= 1
                && block.Part <= 4
                && asset.Kind == ExamMediaKind.Audio
            orderby block.Part, block.FirstQuestionNumber
            select new ExamAttemptListeningBlockSource(
                block.Id,
                block.Part,
                block.FirstQuestionNumber,
                asset.Id,
                asset.ObjectKey,
                asset.DurationMs)
        ).ToListAsync(cancellationToken);

        return new ExamAttemptListeningPlaylistSource(attempt.Status, attempt.IsListeningLocked, blocks);
    }

    public async Task<Guid?> FindActiveAttemptIdAsync(Guid userId, Guid examId, CancellationToken cancellationToken)
    {
        var id = await (
            from a in db.ExamAttempts
            join r in db.ExamRevisions on a.RevisionId equals r.Id
            where a.UserId == userId && r.ExamId == examId && a.Status == ExamAttemptStatus.InProgress
            orderby a.StartedAtUtc descending
            select (Guid?)a.Id
        ).FirstOrDefaultAsync(cancellationToken);

        return id;
    }

    public async Task<(IReadOnlyList<ExamAttemptHistoryItemDto> Items, int TotalCount)> GetAttemptHistoryAsync(
        Guid userId,
        int pageIndex,
        int pageSize,
        Guid? examId,
        CancellationToken cancellationToken)
    {
        var query = db.ExamAttempts
            .AsNoTracking()
            .Where(a => a.UserId == userId);

        if (examId.HasValue)
        {
            query = query.Where(a => db.ExamRevisions.Any(r => r.Id == a.RevisionId && r.ExamId == examId.Value));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await (
            from a in query
            join r in db.ExamRevisions on a.RevisionId equals r.Id
            join e in db.Exams on r.ExamId equals e.Id
            orderby a.StartedAtUtc descending
            select new
            {
                Attempt = a,
                ExamId = e.Id,
                ExamTitle = e.Title,
                ExamCode = e.Code
            }
        )
        .Skip((pageIndex - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

        var items = rawItems.Select(x => new ExamAttemptHistoryItemDto(
            x.Attempt.Id,
            x.ExamId,
            x.ExamTitle,
            x.ExamCode,
            x.Attempt.Status,
            x.Attempt.ListeningScaledScore,
            x.Attempt.ReadingScaledScore,
            x.Attempt.TotalScaledScore,
            x.Attempt.IsTimeExpired,
            x.Attempt.StartedAtUtc,
            x.Attempt.SubmittedAtUtc,
            x.Attempt.SubmittedAtUtc.HasValue
                ? (int)(x.Attempt.SubmittedAtUtc.Value - x.Attempt.StartedAtUtc).TotalSeconds
                : null
        )).ToList();

        return (items, totalCount);
    }

    public void AddAttempt(ExamAttempt attempt)
    {
        db.ExamAttempts.Add(attempt);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
    }
}

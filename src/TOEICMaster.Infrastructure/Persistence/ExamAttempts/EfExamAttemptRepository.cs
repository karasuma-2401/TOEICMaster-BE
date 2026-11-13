using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.ExamAttempts;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.ExamAttempts;

public sealed class EfExamAttemptRepository(ToeicDbContext db) : IExamAttemptRepository
{
    public async Task<IReadOnlyList<PublishedLrExamDto>> GetPublishedExamsAsync(CancellationToken cancellationToken)
    {
        var revisions = await (
            from revision in db.ExamRevisions.AsNoTracking()
            join exam in db.Exams.AsNoTracking() on revision.ExamId equals exam.Id
            where revision.Status == ExamRevisionStatus.Published
            select new
            {
                Revision = revision,
                Exam = exam,
                Blocks = revision.Blocks.Select(block => new
                {
                    block.Part,
                    QuestionCount = block.Questions.Count
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        return revisions
            .GroupBy(item => item.Exam.Id)
            .Select(group => group.OrderByDescending(item => item.Revision.VersionNumber).First())
            .OrderByDescending(item => item.Revision.CreatedAtUtc)
            .Select(item =>
            {
                var total = item.Blocks.Sum(block => block.QuestionCount);
                var listening = item.Blocks.Where(block => block.Part is >= 1 and <= 4).Sum(block => block.QuestionCount);
                return new PublishedLrExamDto(
                    item.Exam.Id,
                    item.Exam.Code,
                    item.Exam.Title,
                    item.Revision.VersionNumber,
                    total,
                    listening,
                    total - listening,
                    item.Revision.CreatedAtUtc);
            })
            .ToList();
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
            .Include(r => r.Blocks)
                .ThenInclude(b => b.Questions)
            .Include(r => r.Blocks)
                .ThenInclude(b => b.ContextBlocks)
            .Where(r => r.ExamId == examId && r.Status == ExamRevisionStatus.Published)
            .OrderByDescending(r => r.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ExamRevision?> FindRevisionAsync(Guid revisionId, CancellationToken cancellationToken)
    {
        return await db.ExamRevisions
            .Include(r => r.Blocks)
                .ThenInclude(b => b.Questions)
            .Include(r => r.Blocks)
                .ThenInclude(b => b.ContextBlocks)
            .FirstOrDefaultAsync(r => r.Id == revisionId, cancellationToken);
    }

    public async Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken)
    {
        return await db.ScoreProfiles
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

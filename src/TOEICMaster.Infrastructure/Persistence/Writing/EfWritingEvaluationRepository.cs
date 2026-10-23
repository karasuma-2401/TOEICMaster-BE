using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Writing.Evaluations;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Writing;

public sealed class EfWritingEvaluationRepository(ToeicDbContext context) : IWritingEvaluationRepository
{
    public Task<WritingEvaluation?> GetByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken) =>
        context.WritingEvaluations.AsNoTracking().Include(evaluation => evaluation.Feedback)
            .SingleOrDefaultAsync(evaluation => evaluation.WritingAttemptId == attemptId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, WritingEvaluation>> GetByAttemptIdsAsync(
        IReadOnlyCollection<Guid> attemptIds, CancellationToken cancellationToken)
    {
        if (attemptIds.Count == 0)
            return new Dictionary<Guid, WritingEvaluation>();
        return await context.WritingEvaluations.AsNoTracking().Where(evaluation => attemptIds.Contains(evaluation.WritingAttemptId))
            .ToDictionaryAsync(evaluation => evaluation.WritingAttemptId, cancellationToken);
    }

    public Task<WritingEvaluation?> FindForUpdateByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken) =>
        context.WritingEvaluations.Include(evaluation => evaluation.Feedback)
            .SingleOrDefaultAsync(evaluation => evaluation.WritingAttemptId == attemptId, cancellationToken);

    public Task<WritingEvaluation?> FindForUpdateAsync(Guid evaluationId, CancellationToken cancellationToken) =>
        context.WritingEvaluations.Include(evaluation => evaluation.Feedback)
            .SingleOrDefaultAsync(evaluation => evaluation.Id == evaluationId, cancellationToken);

    public Task<bool> HasAnonymousConsentAfterAsync(string anonymousSessionHash, Guid excludingAttemptId,
        DateTimeOffset afterUtc, CancellationToken cancellationToken) =>
        (from evaluation in context.WritingEvaluations.AsNoTracking()
         join attempt in context.WritingAttempts.AsNoTracking() on evaluation.WritingAttemptId equals attempt.Id
         where attempt.AnonymousSessionHash == anonymousSessionHash && evaluation.WritingAttemptId != excludingAttemptId &&
               evaluation.ConsentAcceptedAtUtc >= afterUtc
         select evaluation.Id).AnyAsync(cancellationToken);

    public Task AddAsync(WritingEvaluation evaluation, CancellationToken cancellationToken)
    {
        context.WritingEvaluations.Add(evaluation);
        return Task.CompletedTask;
    }

    public async Task<WritingEvaluationWork?> ClaimNextAsync(DateTimeOffset now, DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        var candidate = await context.WritingEvaluations.Where(evaluation =>
                (evaluation.Status == WritingEvaluationStatus.Queued || evaluation.Status == WritingEvaluationStatus.Retrying) &&
                evaluation.NextAttemptAtUtc <= now ||
                evaluation.Status == WritingEvaluationStatus.Processing && evaluation.LeaseExpiresAtUtc <= now)
            .OrderBy(evaluation => evaluation.NextAttemptAtUtc ?? evaluation.LeaseExpiresAtUtc)
            .ThenBy(evaluation => evaluation.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (candidate is null)
            return null;

        try
        {
            candidate.BeginProcessing(now, leaseExpiresAtUtc);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return null;
        }

        var questions = await context.WritingAttemptQuestions.AsNoTracking()
            .Where(question => question.WritingAttemptId == candidate.WritingAttemptId).OrderBy(question => question.Position)
            .Select(question => new WritingEvaluationAnswer(question.Position, (int)question.Part,
                question.PromptSnapshotJson, question.ReviewSnapshotJson, question.Answer)).ToArrayAsync(cancellationToken);
        return new WritingEvaluationWork(candidate.Id, candidate.Provider, candidate.Model, candidate.RubricVersion, questions);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new WritingEvaluatorException("CONCURRENT_UPDATE",
                "Kết quả chấm AI đang được cập nhật bởi một tiến trình khác.", true);
        }
    }
}

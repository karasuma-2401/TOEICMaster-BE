using Microsoft.EntityFrameworkCore;
using Npgsql;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Evaluations;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Speaking;

public sealed class EfSpeakingEvaluationRepository(ToeicDbContext context) : ISpeakingEvaluationRepository
{
    private DbSet<SpeakingEvaluation> Evaluations => context.Set<SpeakingEvaluation>();

    public Task<SpeakingEvaluation?> GetByAttemptIdAsync(Guid attemptId, CancellationToken cancellationToken) =>
        Evaluations.AsNoTracking().Include(evaluation => evaluation.Items)
            .SingleOrDefaultAsync(evaluation => evaluation.AttemptId == attemptId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, SpeakingEvaluation>> GetByAttemptIdsAsync(
        IReadOnlyCollection<Guid> attemptIds, CancellationToken cancellationToken) =>
        attemptIds.Count == 0
            ? new Dictionary<Guid, SpeakingEvaluation>()
            : await Evaluations.AsNoTracking().Where(evaluation => attemptIds.Contains(evaluation.AttemptId))
                .ToDictionaryAsync(evaluation => evaluation.AttemptId, cancellationToken);

    public Task<SpeakingEvaluation?> FindForUpdateByAttemptIdAsync(Guid attemptId,
        CancellationToken cancellationToken) =>
        Evaluations.Include(evaluation => evaluation.Items)
            .SingleOrDefaultAsync(evaluation => evaluation.AttemptId == attemptId, cancellationToken);

    public Task<SpeakingEvaluation?> FindForUpdateAsync(Guid evaluationId, CancellationToken cancellationToken) =>
        Evaluations.Include(evaluation => evaluation.Items)
            .SingleOrDefaultAsync(evaluation => evaluation.Id == evaluationId, cancellationToken);

    public Task<bool> HasAnonymousConsentAfterAsync(string anonymousSessionHash, Guid excludingAttemptId,
        DateTimeOffset afterUtc, CancellationToken cancellationToken) =>
        (from evaluation in Evaluations.AsNoTracking()
         join attempt in context.SpeakingAttempts.AsNoTracking() on evaluation.AttemptId equals attempt.Id
         where attempt.AnonymousSessionHash == anonymousSessionHash && evaluation.AttemptId != excludingAttemptId &&
               evaluation.ConsentAcceptedAtUtc >= afterUtc
         select evaluation.Id).AnyAsync(cancellationToken);

    public void Add(SpeakingEvaluation evaluation) => Evaluations.Add(evaluation);

    public async Task<SpeakingEvaluationWork?> ClaimNextAsync(DateTimeOffset now, DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        var candidate = await Evaluations.Where(evaluation =>
                (evaluation.Status == SpeakingEvaluationStatus.Queued ||
                 evaluation.Status == SpeakingEvaluationStatus.Retrying) && evaluation.NextAttemptAtUtc <= now ||
                evaluation.Status == SpeakingEvaluationStatus.Processing && evaluation.LeaseExpiresAtUtc <= now)
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

        var questions = await (from question in context.SpeakingAttemptQuestions.AsNoTracking()
                               join asset in context.SpeakingMediaAssets.AsNoTracking()
                                   on question.RecordingAssetId equals asset.Id into recordings
                               from recording in recordings.DefaultIfEmpty()
                               where question.AttemptId == candidate.AttemptId
                               orderby question.Position
                               select new
                               {
                                   question.Position,
                                   question.Part,
                                   question.QuestionIndex,
                                   question.ContentJson,
                                   question.ReviewJson,
                                   ObjectKey = recording == null ? null : recording.ObjectKey,
                                   ContentType = recording == null ? null : recording.ContentType
                               }).ToListAsync(cancellationToken);
        return new SpeakingEvaluationWork(candidate.Id, candidate.SpeechLocale, candidate.LanguageModel,
            candidate.RubricVersion, questions.Select(question => new SpeakingEvaluationQuestion(question.Position,
                question.Part, question.QuestionIndex, question.ContentJson, question.ReviewJson,
                question.ObjectKey is null
                    ? null
                    : new SpeakingRecordingReference(question.ObjectKey, question.ContentType!))).ToArray());
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new SpeakingConcurrencyException();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            throw new SpeakingConflictException("Yêu cầu chấm AI cho lượt làm này đã được gửi.");
        }
    }
}

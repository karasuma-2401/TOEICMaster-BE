using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Admin;
using TOEICMaster.Application.Speaking.Attempts;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Speaking;

public sealed class EfSpeakingAttemptRepository(ToeicDbContext context) : ISpeakingAttemptRepository
{
    public async Task<SpeakingPageDto<SpeakingTestWithSlots>> GetPublishedTestPageAsync(string? query,
        int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var tests = context.SpeakingTests.AsNoTracking().Where(test => test.Status == SpeakingTestStatus.Published);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{EfSpeakingAdminRepository.EscapeLike(query.Trim())}%";
            tests = tests.Where(test => EF.Functions.ILike(test.Code, pattern, "\\") ||
                                        EF.Functions.ILike(test.Title, pattern, "\\"));
        }
        var totalCount = await tests.CountAsync(cancellationToken);
        var items = await tests.OrderByDescending(test => test.PublishedAtUtc).ThenBy(test => test.Id)
            .Skip(checked((pageNumber - 1) * pageSize)).Take(pageSize).ToListAsync(cancellationToken);
        var ids = items.Select(test => test.Id).ToArray();
        var slots = (await context.SpeakingTestExercises.AsNoTracking().Where(slot => ids.Contains(slot.TestId))
                .ToListAsync(cancellationToken))
            .ToLookup(slot => slot.TestId);
        return new SpeakingPageDto<SpeakingTestWithSlots>(items.Select(test =>
            new SpeakingTestWithSlots(test, slots[test.Id].ToArray())).ToArray(), pageNumber, pageSize, totalCount);
    }

    public async Task<SpeakingTestWithSlots?> FindPublishedTestAsync(Guid testId, CancellationToken cancellationToken)
    {
        var test = await context.SpeakingTests.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == testId && item.Status == SpeakingTestStatus.Published, cancellationToken);
        if (test is null)
            return null;
        var slots = await context.SpeakingTestExercises.AsNoTracking().Where(slot => slot.TestId == testId)
            .OrderBy(slot => slot.Part).ThenBy(slot => slot.SequenceNumber).ToListAsync(cancellationToken);
        return new SpeakingTestWithSlots(test, slots);
    }

    public async Task<SpeakingMediaAsset?> FindPublishedImageAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var assetText = assetId.ToString();
        // The frozen snapshot decides which picture a published test shows, even if the exercise changed later.
        var published = await context.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM speaking_test_exercises AS slot
                JOIN speaking_tests AS test ON test.id = slot.test_id
                WHERE test.status = 'PUBLISHED' AND slot.snapshot_json ->> 'imageAssetId' = {assetText}
            ) AS "Value"
            """).SingleAsync(cancellationToken);
        return published
            ? await context.SpeakingMediaAssets.AsNoTracking().SingleOrDefaultAsync(asset =>
                asset.Id == assetId && asset.Kind == SpeakingMediaKind.Image, cancellationToken)
            : null;
    }

    public Task<SpeakingAttempt?> FindAttemptForUpdateAsync(Guid attemptId, CancellationToken cancellationToken) =>
        context.SpeakingAttempts.Include(attempt => attempt.Questions.OrderBy(question => question.Position))
            .SingleOrDefaultAsync(attempt => attempt.Id == attemptId, cancellationToken);

    public async Task<IReadOnlyList<SpeakingAttempt>> GetAttemptsAsync(SpeakingAttemptOwner owner, int take,
        CancellationToken cancellationToken)
    {
        var attempts = context.SpeakingAttempts.AsNoTracking().Include(attempt => attempt.Questions).AsQueryable();
        attempts = owner.UserId is { } userId
            ? attempts.Where(attempt => attempt.OwnerUserId == userId)
            : attempts.Where(attempt => attempt.AnonymousSessionHash == owner.AnonymousSessionHash);
        return await attempts.OrderByDescending(attempt => attempt.CreatedAtUtc).ThenByDescending(attempt => attempt.Id)
            .Take(take).AsSplitQuery().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, SpeakingTestHeader>> GetTestHeadersAsync(
        IReadOnlyCollection<Guid> testIds, CancellationToken cancellationToken) =>
        testIds.Count == 0
            ? new Dictionary<Guid, SpeakingTestHeader>()
            : await context.SpeakingTests.AsNoTracking().Where(test => testIds.Contains(test.Id))
                .ToDictionaryAsync(test => test.Id, test => new SpeakingTestHeader(test.Code, test.Title),
                    cancellationToken);

    public void AddAttempt(SpeakingAttempt attempt) => context.SpeakingAttempts.Add(attempt);

    public Task<SpeakingMediaAsset?> FindMediaAssetAsync(Guid assetId, CancellationToken cancellationToken) =>
        context.SpeakingMediaAssets.SingleOrDefaultAsync(asset => asset.Id == assetId, cancellationToken);

    public void AddMediaAsset(SpeakingMediaAsset asset) => context.SpeakingMediaAssets.Add(asset);

    public void RemoveMediaAsset(SpeakingMediaAsset asset) => context.SpeakingMediaAssets.Remove(asset);

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
    }

    public void ClearTracking() => context.ChangeTracker.Clear();
}

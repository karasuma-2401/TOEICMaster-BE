using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Writing.Attempts;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Writing;

public sealed class EfWritingAttemptRepository(ToeicDbContext context) : IWritingAttemptRepository
{
    public async Task<WritingPublicTestPageDto> GetPublishedTestsAsync(string? query, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<WritingTest> tests = context.WritingTests.AsNoTracking().Include(test => test.Questions)
            .Where(test => test.Status == WritingTestStatus.Published);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToUpperInvariant();
            tests = tests.Where(test => test.Code.ToUpper().Contains(term) || test.Title.ToUpper().Contains(term));
        }

        var totalCount = await tests.CountAsync(cancellationToken);
        var offset = checked((pageNumber - 1) * pageSize);
        var items = await tests.OrderByDescending(test => test.PublishedAtUtc).ThenBy(test => test.Id).Skip(offset)
            .Take(pageSize).ToListAsync(cancellationToken);
        return new WritingPublicTestPageDto(items.Select(ToPublicSummary).ToArray(), pageNumber, pageSize, totalCount);
    }

    public async Task<WritingPublicTestDto?> GetPublishedTestAsync(Guid testId, CancellationToken cancellationToken)
    {
        var test = await context.WritingTests.AsNoTracking().Include(item => item.Questions).SingleOrDefaultAsync(item =>
            item.Id == testId && item.Status == WritingTestStatus.Published, cancellationToken);
        return test is null ? null : ToPublicDetail(test);
    }

    public Task<WritingTest?> FindPublishedTestForAttemptAsync(Guid testId, CancellationToken cancellationToken) =>
        context.WritingTests.Include(test => test.Questions).SingleOrDefaultAsync(test =>
            test.Id == testId && test.Status == WritingTestStatus.Published, cancellationToken);

    public Task<WritingMediaAsset?> FindPublishedMediaAssetAsync(Guid assetId, CancellationToken cancellationToken) =>
        (from asset in context.WritingMediaAssets.AsNoTracking()
         join exercise in context.WritingExercises.AsNoTracking() on asset.Id equals exercise.ImageAssetId
         join question in context.WritingTestQuestions.AsNoTracking() on exercise.Id equals question.ExerciseId
         join test in context.WritingTests.AsNoTracking() on question.WritingTestId equals test.Id
         where asset.Id == assetId && test.Status == WritingTestStatus.Published
         select asset).FirstOrDefaultAsync(cancellationToken);

    public Task<WritingAttempt?> FindAttemptForUpdateAsync(Guid attemptId, CancellationToken cancellationToken) =>
        context.WritingAttempts.Include(attempt => attempt.Questions).SingleOrDefaultAsync(attempt => attempt.Id == attemptId,
            cancellationToken);

    public Task<WritingAttempt?> GetAttemptAsync(Guid attemptId, CancellationToken cancellationToken) =>
        context.WritingAttempts.AsNoTracking().Include(attempt => attempt.Questions)
            .SingleOrDefaultAsync(attempt => attempt.Id == attemptId, cancellationToken);

    public async Task<IReadOnlyList<WritingAttempt>> GetAttemptsAsync(WritingAttemptOwner owner,
        CancellationToken cancellationToken)
    {
        IQueryable<WritingAttempt> attempts = context.WritingAttempts.AsNoTracking().Include(attempt => attempt.Questions);
        attempts = owner.UserId is not null
            ? attempts.Where(attempt => attempt.UserId == owner.UserId)
            : attempts.Where(attempt => attempt.AnonymousSessionHash == owner.AnonymousSessionHash);
        return await attempts.OrderByDescending(attempt => attempt.UpdatedAtUtc).ThenByDescending(attempt => attempt.Id)
            .Take(100).ToListAsync(cancellationToken);
    }

    public Task AddAttemptAsync(WritingAttempt attempt, CancellationToken cancellationToken)
    {
        context.WritingAttempts.Add(attempt);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new WritingAttemptConflictException("Lượt làm bài đã được cập nhật ở một phiên khác. Hãy tải lại trang.");
        }
    }

    private static WritingPublicTestSummaryDto ToPublicSummary(WritingTest test) => new(test.Id, test.Code, test.Title,
        test.Description, Parts(test), test.PublishedAtUtc!.Value);

    private static WritingPublicTestDto ToPublicDetail(WritingTest test) => new(test.Id, test.Code, test.Title,
        test.Description, Parts(test), test.PublishedAtUtc!.Value);

    private static IReadOnlyList<WritingAttemptPartDto> Parts(WritingTest test) => test.Questions
        .GroupBy(question => question.Part).OrderBy(group => group.Key).Select(WritingAttemptMapping.ToPartDto).ToArray();
}

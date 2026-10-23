using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Admin;
using TOEICMaster.Application.Speaking.Attempts;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.TestDoubles;

/// <summary>In-memory Speaking persistence and storage shared by application and HTTP contract tests.</summary>
public sealed class InMemorySpeaking : ISpeakingAdminRepository, ISpeakingAttemptRepository, ISpeakingMediaStore
{
    private readonly object _gate = new();
    public List<SpeakingExercise> Exercises { get; } = [];
    public List<SpeakingMediaAsset> Assets { get; } = [];
    public List<SpeakingTest> Tests { get; } = [];
    public List<SpeakingTestExercise> Slots { get; } = [];
    public List<SpeakingAttempt> Attempts { get; } = [];
    public Dictionary<(SpeakingMediaKind Kind, string Key), byte[]> Objects { get; } = [];
    /// <summary>Simulates another request saving first: the next save throws a concurrency conflict.</summary>
    public int PendingConcurrencyConflicts { get; set; }

    public Task<SpeakingAdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SpeakingAdminSummaryDto(
            new SpeakingExerciseCountsDto(Exercises.Count,
                Exercises.Count(item => item.Status == SpeakingExerciseStatus.Published),
                Exercises.Count(item => item.Status == SpeakingExerciseStatus.Draft),
                Exercises.Count(item => item.Status == SpeakingExerciseStatus.Archived)),
            new SpeakingTestCountsDto(Tests.Count, Tests.Count(test => test.Status == SpeakingTestStatus.Published),
                Tests.Count(test => test.Status == SpeakingTestStatus.Draft))));

    public Task<SpeakingPageDto<SpeakingExercise>> GetExercisePageAsync(SpeakingExerciseFilter filter,
        CancellationToken cancellationToken)
    {
        var items = Exercises.Where(item => filter.Part is null || item.Part == filter.Part)
            .Where(item => string.IsNullOrWhiteSpace(filter.Query) ||
                           item.Code.Contains(filter.Query.Trim(), StringComparison.OrdinalIgnoreCase) ||
                           item.Topic.Contains(filter.Query.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(item => !SpeakingCodes.TryParseDifficulty(filter.Difficulty, out var difficulty) ||
                           item.Difficulty == difficulty)
            .Where(item => !SpeakingCodes.TryParseExerciseStatus(filter.Status, out var status) || item.Status == status)
            .OrderByDescending(item => item.UpdatedAtUtc).ThenBy(item => item.Id).ToArray();
        return Task.FromResult(new SpeakingPageDto<SpeakingExercise>(items
            .Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize).ToArray(), filter.PageNumber,
            filter.PageSize, items.Length));
    }

    public Task<SpeakingExercise?> FindExerciseAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Exercises.SingleOrDefault(item => item.Id == id));

    public Task<SpeakingExercise?> FindExerciseForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        FindExerciseAsync(id, cancellationToken);

    public Task<IReadOnlyList<SpeakingExercise>> FindExercisesAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpeakingExercise>>(Exercises.Where(item => ids.Contains(item.Id)).ToArray());

    public Task<bool> ExerciseCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(Exercises.Any(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase) &&
                                              item.Id != excludingId));

    public void AddExercise(SpeakingExercise exercise) => Exercises.Add(exercise);

    public Task<SpeakingMediaAsset?> FindMediaAssetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Assets.SingleOrDefault(asset => asset.Id == id));

    public void AddMediaAsset(SpeakingMediaAsset asset) => Assets.Add(asset);

    public void RemoveMediaAsset(SpeakingMediaAsset asset) => Assets.Remove(asset);

    public Task<SpeakingPageDto<SpeakingTestWithSlots>> GetTestPageAsync(SpeakingTestFilter filter,
        CancellationToken cancellationToken)
    {
        var items = Tests.Where(test => !SpeakingCodes.TryParseTestStatus(filter.Status, out var status) ||
                                        test.Status == status)
            .OrderByDescending(test => test.UpdatedAtUtc).Select(WithSlots).ToArray();
        return Task.FromResult(new SpeakingPageDto<SpeakingTestWithSlots>(items
            .Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize).ToArray(), filter.PageNumber,
            filter.PageSize, items.Length));
    }

    public Task<SpeakingTestWithSlots?> FindTestAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Tests.Where(test => test.Id == id).Select(WithSlots).SingleOrDefault());

    public Task<SpeakingTestWithSlots?> FindTestForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        FindTestAsync(id, cancellationToken);

    public Task<bool> TestCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(Tests.Any(test => string.Equals(test.Code, code, StringComparison.OrdinalIgnoreCase) &&
                                          test.Id != excludingId));

    public void AddTest(SpeakingTest test, IReadOnlyList<SpeakingTestExercise> slots)
    {
        Tests.Add(test);
        Slots.AddRange(slots);
    }

    public Task ReplaceDraftSlotsAndSaveAsync(SpeakingTest test, IReadOnlyList<SpeakingTestExercise> slots,
        CancellationToken cancellationToken)
    {
        Slots.RemoveAll(slot => slot.TestId == test.Id);
        Slots.AddRange(slots);
        return Task.CompletedTask;
    }

    public Task<SpeakingPageDto<SpeakingTestWithSlots>> GetPublishedTestPageAsync(string? query, int pageNumber,
        int pageSize, CancellationToken cancellationToken)
    {
        var items = Tests.Where(test => test.Status == SpeakingTestStatus.Published).Select(WithSlots).ToArray();
        return Task.FromResult(new SpeakingPageDto<SpeakingTestWithSlots>(items, pageNumber, pageSize, items.Length));
    }

    public Task<SpeakingTestWithSlots?> FindPublishedTestAsync(Guid testId, CancellationToken cancellationToken) =>
        Task.FromResult(Tests.Where(test => test.Id == testId && test.Status == SpeakingTestStatus.Published)
            .Select(WithSlots).SingleOrDefault());

    public Task<SpeakingMediaAsset?> FindPublishedImageAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var published = Slots.Any(slot => slot.SnapshotJson?.Contains(assetId.ToString(), StringComparison.Ordinal)
                                          == true && Tests.Single(test => test.Id == slot.TestId).Status ==
                                          SpeakingTestStatus.Published);
        return Task.FromResult(published ? Assets.SingleOrDefault(asset => asset.Id == assetId) : null);
    }

    public Task<SpeakingAttempt?> FindAttemptForUpdateAsync(Guid attemptId, CancellationToken cancellationToken) =>
        Task.FromResult(Attempts.SingleOrDefault(attempt => attempt.Id == attemptId));

    public Task<IReadOnlyList<SpeakingAttempt>> GetAttemptsAsync(SpeakingAttemptOwner owner, int take,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpeakingAttempt>>(Attempts
            .Where(attempt => attempt.IsOwnedBy(owner.UserId, owner.AnonymousSessionHash))
            .OrderByDescending(attempt => attempt.CreatedAtUtc).Take(take).ToArray());

    public Task<IReadOnlyDictionary<Guid, SpeakingTestHeader>> GetTestHeadersAsync(
        IReadOnlyCollection<Guid> testIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, SpeakingTestHeader>>(Tests.Where(test => testIds.Contains(test.Id))
            .ToDictionary(test => test.Id, test => new SpeakingTestHeader(test.Code, test.Title)));

    public void AddAttempt(SpeakingAttempt attempt) => Attempts.Add(attempt);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (PendingConcurrencyConflicts <= 0)
                return Task.CompletedTask;
            PendingConcurrencyConflicts--;
        }
        throw new SpeakingConcurrencyException();
    }

    public void ClearTracking()
    {
    }

    public Task UploadAsync(SpeakingMediaKind kind, string objectKey, byte[] content, string contentType,
        CancellationToken cancellationToken)
    {
        Objects[(kind, objectKey)] = content;
        return Task.CompletedTask;
    }

    public Task<byte[]> DownloadAsync(SpeakingMediaKind kind, string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(Objects[(kind, objectKey)]);

    public Task DeleteAsync(SpeakingMediaKind kind, string objectKey, CancellationToken cancellationToken)
    {
        Objects.Remove((kind, objectKey));
        return Task.CompletedTask;
    }

    private SpeakingTestWithSlots WithSlots(SpeakingTest test) => new(test,
        Slots.Where(slot => slot.TestId == test.Id).OrderBy(slot => slot.Part).ThenBy(slot => slot.SequenceNumber)
            .ToArray());
}

/// <summary>Valid inputs and media bytes for Speaking tests.</summary>
public static class SpeakingSamples
{
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lWQAAAAASUVORK5CYII=");

    public static readonly byte[] WebmAudio = [0x1a, 0x45, 0xdf, 0xa3, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06];

    public static SpeakingExerciseInput Exercise(int part, string code, Guid? imageAssetId = null,
        string status = "PUBLISHED") => part switch
        {
            1 => Common(part, code, status) with { Passage = "Attention, shoppers. The store closes in thirty minutes." },
            2 => Common(part, code, status) with
            {
                ImageAssetId = imageAssetId,
                ImageAlt = "Three colleagues at a meeting table."
            },
            3 => Common(part, code, status) with
            {
                Context = "Imagine a marketing firm is interviewing you about reading habits.",
                Questions = ["How often do you read?", "Printed or e-books?", "Recommend a book."]
            },
            4 => Common(part, code, status) with
            {
                DocumentTitle = "Marketing Conference · May 14",
                DocumentRows = [new("9:00", "Registration"), new("9:30", "Keynote")],
                CallerIntro = "Hi, I lost my schedule.",
                Questions = ["Where is it held?", "Is lunch in the main hall?", "Sessions after lunch?"]
            },
            5 => Common(part, code, status) with { Prompt = "Should employees work from home? Give reasons." },
            _ => throw new ArgumentOutOfRangeException(nameof(part))
        };

    private static SpeakingExerciseInput Common(int part, string code, string status) =>
        new(part, code, "Công việc", "MEDIUM", status, "Rõ ràng, đúng trọng tâm.", $"Model answer {code}.");
}

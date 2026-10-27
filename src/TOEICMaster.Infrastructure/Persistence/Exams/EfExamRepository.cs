using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Exams;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Exams;

public sealed class EfExamRepository(ToeicDbContext context) : IExamRepository
{
    public Task<bool> ExamCodeExistsAsync(string code, CancellationToken cancellationToken) => context.Exams
        .AnyAsync(exam => exam.Code == code, cancellationToken);

    public Task<Exam?> FindExamAsync(Guid examId, CancellationToken cancellationToken) => context.Exams
        .Include(exam => exam.Revisions).ThenInclude(revision => revision.Blocks)
        .ThenInclude(block => block.Questions)
        .Include(exam => exam.Revisions).ThenInclude(revision => revision.Blocks)
        .ThenInclude(block => block.ContextBlocks)
        .SingleOrDefaultAsync(exam => exam.Id == examId, cancellationToken);

    public async Task<ExamPageDto> GetExamPageAsync(ExamRevisionStatus? status, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        var exams = context.Exams.AsNoTracking();
        if (status is { } requiredStatus)
            exams = exams.Where(exam => exam.Revisions.OrderByDescending(revision => revision.VersionNumber)
                .Select(revision => revision.Status).First() == requiredStatus);

        var totalCount = await exams.CountAsync(cancellationToken);
        var offset = checked((pageNumber - 1) * pageSize);
        if (offset >= totalCount)
            return new ExamPageDto([], pageNumber, pageSize, totalCount);

        var items = await exams.OrderByDescending(exam => exam.CreatedAtUtc).ThenBy(exam => exam.Id).Skip(offset)
            .Take(pageSize).Select(exam => new ExamListItemDto(exam.Id, exam.Code, exam.Title,
                exam.Revisions.OrderByDescending(revision => revision.VersionNumber).Select(revision => revision.Id).First(),
                exam.Revisions.Max(revision => revision.VersionNumber),
                exam.Revisions.OrderByDescending(revision => revision.VersionNumber).Select(revision => revision.Status).First(),
                exam.CreatedAtUtc)).ToListAsync(cancellationToken);
        return new ExamPageDto(items, pageNumber, pageSize, totalCount);
    }

    public Task<ScoreProfile?> FindScoreProfileAsync(Guid scoreProfileId, CancellationToken cancellationToken) => context
        .ScoreProfiles.Include(profile => profile.Conversions).SingleOrDefaultAsync(profile => profile.Id == scoreProfileId,
            cancellationToken);

    public async Task<IReadOnlyList<ScoreProfileDto>> GetScoreProfilesAsync(CancellationToken cancellationToken) =>
        await context.ScoreProfiles.AsNoTracking().OrderBy(profile => profile.Name).ThenBy(profile => profile.Id)
            .Select(profile => new ScoreProfileDto(profile.Id, profile.Name,
                profile.Conversions.Count == 202 && profile.Conversions.Count(conversion =>
                    conversion.Skill == ExamSkill.Listening) == 101 && profile.Conversions.Count(conversion =>
                    conversion.Skill == ExamSkill.Reading) == 101, profile.Conversions.Count)).ToListAsync(cancellationToken);

    public async Task<bool> ImageAssetsExistAsync(IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0)
            return true;
        var distinctAssetIds = assetIds.Distinct().ToArray();
        return await context.ExamMediaAssets.CountAsync(asset => distinctAssetIds.Contains(asset.Id), cancellationToken) ==
            distinctAssetIds.Length;
    }

    public Task AddExamAsync(Exam exam, CancellationToken cancellationToken)
    {
        context.Exams.Add(exam);
        return Task.CompletedTask;
    }

    public Task AddScoreProfileAsync(ScoreProfile scoreProfile, CancellationToken cancellationToken)
    {
        context.ScoreProfiles.Add(scoreProfile);
        return Task.CompletedTask;
    }

    public Task AddExamMediaAssetAsync(ExamMediaAsset asset, CancellationToken cancellationToken)
    {
        context.ExamMediaAssets.Add(asset);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);
}

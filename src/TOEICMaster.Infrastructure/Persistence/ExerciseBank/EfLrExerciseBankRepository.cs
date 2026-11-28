using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.ExerciseBank;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.ExerciseBank;

public sealed class EfLrExerciseBankRepository(ToeicDbContext context) : ILrExerciseBankRepository
{
    public Task<LrExerciseBankItem?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken) =>
        context.LrExerciseBankItems.Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);
    public async Task<LrExerciseItemPageDto> GetPageAsync(
        LrExerciseItemFilter filter,
        CancellationToken cancellationToken)
    {
        IQueryable<LrExerciseBankItem> query = context.LrExerciseBankItems
            .AsNoTracking()
            .Include(item => item.Questions)
            .Where(item => item.Part == LrExerciseBankMapping.ParsePart(filter.Part));
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.Code.ToUpper().Contains(term) ||
                item.Topic.ToUpper().Contains(term) ||
                item.Questions.Any(question =>
                    question.Code.ToUpper().Contains(term) ||
                    question.Prompt.ToUpper().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(filter.Difficulty))
            query = query.Where(item =>
                item.Difficulty == LrExerciseBankMapping.ParseDifficulty(filter.Difficulty));
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(item => item.Status == LrExerciseBankMapping.ParseStatus(filter.Status));

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = checked((filter.PageNumber - 1) * filter.PageSize);
        var items = await query
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Id)
            .Skip(offset)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new LrExerciseItemPageDto(
            items.Select(LrExerciseBankMapping.ToDto).ToArray(),
            filter.PageNumber,
            filter.PageSize,
            totalCount);
    }

    public async Task<(IReadOnlyList<LrExerciseBankItem> Items, int TotalCount)> GetPublishedPageAsync(
        int part,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var parsedPart = LrExerciseBankMapping.ParsePart(part);
        var query = context.LrExerciseBankItems
            .AsNoTracking()
            .Include(item => item.Questions)
            .Where(item =>
                item.Part == parsedPart &&
                item.Status == LrExerciseStatus.Published &&
                (item.Part >= LrExercisePart.Part5 ||
                 item.AudioUrl != null && (item.Part != LrExercisePart.Part1 || item.ImageUrl != null)));

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = checked((pageNumber - 1) * pageSize);
        var items = await query
            .OrderBy(item => item.Code)
            .ThenBy(item => item.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<LrExerciseBankItem?> GetPublishedByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        return context.LrExerciseBankItems
            .AsNoTracking()
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(
                item => item.Id == itemId &&
                        item.Status == LrExerciseStatus.Published &&
                        (item.Part >= LrExercisePart.Part5 ||
                         item.AudioUrl != null && (item.Part != LrExercisePart.Part1 || item.ImageUrl != null)),
                cancellationToken);
    }
    public async Task<LrExerciseBankSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var summaries = await context.LrExerciseBankItems.AsNoTracking().GroupBy(item => item.Part)
            .Select(group => new
            {
                Part = group.Key,
                ItemCount = group.Count(),
                QuestionCount = group.SelectMany(item => item.Questions).Count(),
                UpdatedAtUtc = group.Max(item => item.UpdatedAtUtc)
            }).ToDictionaryAsync(summary => summary.Part, cancellationToken);
        var parts = Enum.GetValues<LrExercisePart>().Select(part =>
        {
            var summary = summaries.GetValueOrDefault(part);
            return new LrExercisePartSummaryDto((int)part, LrExerciseBankItem.GetKind(part).ToApiValue(),
                summary?.ItemCount ?? 0, summary?.QuestionCount ?? 0, summary?.UpdatedAtUtc);
        }).ToArray();
        return new LrExerciseBankSummaryDto(parts.Sum(part => part.QuestionCount),
            parts.Where(part => part.UpdatedAtUtc.HasValue).Select(part => part.UpdatedAtUtc).Max(), parts);
    }

    public Task<bool> ItemCodeExistsAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return context.LrExerciseBankItems.AsNoTracking().AnyAsync(item => item.Code.ToUpper() == normalized,
            cancellationToken);
    }

    public Task AddItemsAsync(IReadOnlyCollection<LrExerciseBankItem> items, CancellationToken cancellationToken)
    {
        context.LrExerciseBankItems.AddRange(items);
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
            throw new LrExerciseBankConflictException("Exercise bank data was updated by another user.");
        }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new LrExerciseBankConflictException("Exercise item code or question code already exists.");
        }
    }
}

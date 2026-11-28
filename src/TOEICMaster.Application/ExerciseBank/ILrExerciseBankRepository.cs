
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExerciseBank;

public interface ILrExerciseBankRepository
{
    Task<LrExerciseBankItem?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken);
    Task<LrExerciseItemPageDto> GetPageAsync(
        LrExerciseItemFilter filter,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<LrExerciseBankItem> Items, int TotalCount)> GetPublishedPageAsync(
        int part,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<LrExerciseBankItem?> GetPublishedByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken);

    Task<LrExerciseBankSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);

    Task<bool> ItemCodeExistsAsync(string code, CancellationToken cancellationToken);

    Task AddItemsAsync(
        IReadOnlyCollection<LrExerciseBankItem> items,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

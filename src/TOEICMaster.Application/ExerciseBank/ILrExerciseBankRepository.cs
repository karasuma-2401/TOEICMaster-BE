using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.ExerciseBank;

public interface ILrExerciseBankRepository
{
    Task<LrExerciseItemPageDto> GetPageAsync(LrExerciseItemFilter filter, CancellationToken cancellationToken);
    Task<LrExerciseBankSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
    Task<bool> ItemCodeExistsAsync(string code, CancellationToken cancellationToken);
    Task AddItemsAsync(IReadOnlyCollection<LrExerciseBankItem> items, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

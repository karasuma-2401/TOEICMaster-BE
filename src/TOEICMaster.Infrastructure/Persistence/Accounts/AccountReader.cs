using Microsoft.EntityFrameworkCore;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Accounts.Queries.GetAccounts;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Accounts;

public sealed class AccountReader(ToeicDbContext context) : IAccountReader
{
    public async Task<AccountPageDto> GetPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var accounts = context.Users.AsNoTracking();
        var totalCount = await accounts.CountAsync(cancellationToken);
        var offset = checked((pageNumber - 1) * pageSize);
        if (offset >= totalCount)
            return new AccountPageDto([], pageNumber, pageSize, totalCount);

        var items = await BuildPageQuery(accounts, offset, pageSize).ToListAsync(cancellationToken);
        return new AccountPageDto(items, pageNumber, pageSize, totalCount);
    }

    internal static IQueryable<AccountSummaryDto> BuildPageQuery(IQueryable<User> accounts, int offset, int pageSize)
        => accounts.OrderByDescending(account => account.CreatedAtUtc).ThenBy(account => account.Id)
            .Skip(offset).Take(pageSize)
            .Select(account => new AccountSummaryDto(account.Id, account.Email, account.DisplayName,
                account.IsActive, account.CreatedAtUtc));
}

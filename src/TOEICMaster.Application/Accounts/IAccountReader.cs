using TOEICMaster.Application.Accounts.Queries.GetAccounts;

namespace TOEICMaster.Application.Accounts;

public interface IAccountReader
{
    Task<AccountPageDto> GetPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);
}

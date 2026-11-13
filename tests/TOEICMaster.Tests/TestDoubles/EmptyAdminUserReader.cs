using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;

namespace TOEICMaster.Tests.TestDoubles;

public sealed class EmptyAdminUserReader : IAdminUserReader
{
    public Task<AdminUserPageDto> GetPageAsync(GetAdminUsersQuery query, CancellationToken cancellationToken)
        => Task.FromResult(new AdminUserPageDto([], query.Page, query.PageSize, 0, 0, new AdminUserStatsDto(0, 0, 0, 0)));
}

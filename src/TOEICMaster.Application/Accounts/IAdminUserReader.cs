using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;

namespace TOEICMaster.Application.Accounts;

public interface IAdminUserReader
{
    Task<AdminUserPageDto> GetPageAsync(GetAdminUsersQuery query, CancellationToken cancellationToken);
}

using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    Task<Guid?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default);
}

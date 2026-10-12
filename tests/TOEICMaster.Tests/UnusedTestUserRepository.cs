using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests;

// Resolves authentication handlers in existing Application tests, which do not use persistence.
public sealed class UnusedTestUserRepository : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public Task AddAsync(User user, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public Task<Guid?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public Task<IEnumerable<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

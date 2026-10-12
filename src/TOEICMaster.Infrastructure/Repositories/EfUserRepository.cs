using Microsoft.EntityFrameworkCore;
using Npgsql;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;

namespace TOEICMaster.Infrastructure.Repositories;

public sealed class EfUserRepository : IUserRepository
{
    private readonly ToeicDbContext _db;

    public EfUserRepository(ToeicDbContext db) => _db = db;

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbEx) when (dbEx.InnerException is PostgresException pex && pex.SqlState == "23505")
        {
            // Unique violation (duplicate email)
            throw new DuplicateEmailException();
        }
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var ur = new UserRole { UserId = userId, RoleId = roleId };
        _db.UserRoles.Add(ur);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        return role?.Id;
    }
}

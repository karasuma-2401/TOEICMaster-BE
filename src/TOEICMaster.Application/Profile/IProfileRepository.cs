using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Profile;

public interface IProfileRepository
{
    Task<User?> FindAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken);
    Task<bool> UpdateProfileAsync(Guid userId, string displayName, string? phone, DateOnly? dateOfBirth,
        int? targetScore, CancellationToken cancellationToken);
    Task<bool> UpdateDetailsAsync(Guid userId, string displayName, string? phone, int? targetScore,
        CancellationToken cancellationToken);
    Task<bool> UpdateAvatarAsync(Guid userId, string objectKey, CancellationToken cancellationToken);
    Task<string?> ClearAvatarAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> ChangePasswordAsync(Guid userId, string currentHash, string newHash,
        DateTimeOffset changedAtUtc, CancellationToken cancellationToken);
}

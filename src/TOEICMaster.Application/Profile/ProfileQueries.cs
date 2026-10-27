using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Profile;

public sealed record ProfileDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    string? Phone,
    DateOnly? DateOfBirth,
    int? TargetScore,
    string? AvatarVersion,
    bool HasPassword)
{
    public static ProfileDto From(User user)
    {
        var avatarUrl = user.AvatarObjectKey is null ? null : "/api/v1/profile/avatar";
        var avatarVersion = user.AvatarObjectKey is null
            ? null
            : Path.GetFileNameWithoutExtension(user.AvatarObjectKey);
        return new ProfileDto(user.Id, user.DisplayName, user.Email, avatarUrl, user.Phone, user.DateOfBirth,
            user.TargetScore, avatarVersion, user.PasswordHash is not null);
    }
}

public sealed record GetProfileQuery(Guid UserId) : IRequest<ProfileDto>;

public sealed class GetProfileQueryHandler(IProfileRepository repository) : IRequestHandler<GetProfileQuery, ProfileDto>
{
    public async Task<ProfileDto> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await repository.FindAsync(request.UserId, cancellationToken);
        if (user is not { IsActive: true })
            throw new InvalidSessionException();
        return ProfileDto.From(user);
    }
}

public sealed record GetProfileAvatarQuery(Guid UserId) : IRequest<AvatarContent?>;

public sealed class GetProfileAvatarQueryHandler(IProfileRepository repository, IAvatarStore store)
    : IRequestHandler<GetProfileAvatarQuery, AvatarContent?>
{
    public async Task<AvatarContent?> Handle(GetProfileAvatarQuery request, CancellationToken cancellationToken)
    {
        var user = await repository.FindAsync(request.UserId, cancellationToken);
        if (user is not { IsActive: true })
            throw new InvalidSessionException();
        return user.AvatarObjectKey is null
            ? null
            : await store.DownloadAsync(user.AvatarObjectKey, cancellationToken);
    }
}

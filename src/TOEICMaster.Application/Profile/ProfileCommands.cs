using FluentValidation;
using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Application.Profile;

public sealed record UpdateProfileNameCommand(Guid UserId, string DisplayName) : IRequest<ProfileDto>;

public sealed class UpdateProfileNameCommandValidator : AbstractValidator<UpdateProfileNameCommand>
{
    public UpdateProfileNameCommandValidator()
    {
        RuleFor(command => command.DisplayName).Must(name => !string.IsNullOrWhiteSpace(name))
            .MaximumLength(100);
    }
}

public sealed class UpdateProfileNameCommandHandler(IProfileRepository repository)
    : IRequestHandler<UpdateProfileNameCommand, ProfileDto>
{
    public async Task<ProfileDto> Handle(UpdateProfileNameCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.UpdateDisplayNameAsync(request.UserId, request.DisplayName.Trim(), cancellationToken))
            throw new InvalidSessionException();
        var user = await repository.FindAsync(request.UserId, cancellationToken)
            ?? throw new InvalidSessionException();
        return ProfileDto.From(user);
    }
}

public sealed record UpdateProfileCommand(Guid UserId, string DisplayName, string? Phone, DateOnly? DateOfBirth,
    int? TargetScore) : IRequest<ProfileDto>;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(command => command.DisplayName).Must(name => !string.IsNullOrWhiteSpace(name))
            .MaximumLength(100);
        RuleFor(command => command.Phone)
            .Matches("^(0|\\+84)[0-9]{9}$")
            .When(command => command.Phone is not null);
        RuleFor(command => command.DateOfBirth)
            .Must(date => date is null || date.Value >= new DateOnly(1900, 1, 1))
            .WithMessage("Ngày sinh không hợp lệ.")
            .Must(date => date is null || date.Value <= DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("Ngày sinh không được ở tương lai.");
        RuleFor(command => command.TargetScore)
            .Must(score => score is null || score is >= 10 and <= 990 && score % 5 == 0)
            .WithMessage("Điểm TOEIC mục tiêu phải từ 10 đến 990 và là bội số của 5.");
    }
}

public sealed class UpdateProfileCommandHandler(IProfileRepository repository)
    : IRequestHandler<UpdateProfileCommand, ProfileDto>
{
    public async Task<ProfileDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName.Trim();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        if (displayName.Length == 0 || !await repository.UpdateProfileAsync(request.UserId, displayName, phone,
                request.DateOfBirth, request.TargetScore, cancellationToken))
            throw new InvalidSessionException();
        var user = await repository.FindAsync(request.UserId, cancellationToken)
            ?? throw new InvalidSessionException();
        return ProfileDto.From(user);
    }
}

public sealed record ChangeProfilePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest;

public sealed class ChangeProfilePasswordCommandValidator : AbstractValidator<ChangeProfilePasswordCommand>
{
    public ChangeProfilePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword).NotEmpty();
        RuleFor(command => command.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(command => command.NewPassword).NotEqual(command => command.CurrentPassword);
    }
}

public sealed class ChangeProfilePasswordCommandHandler(
    IProfileRepository repository, IPasswordHasher passwordHasher, TimeProvider timeProvider)
    : IRequestHandler<ChangeProfilePasswordCommand>
{
    public async Task Handle(ChangeProfilePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await repository.FindAsync(request.UserId, cancellationToken);
        if (user is not { IsActive: true } || user.PasswordHash is null ||
            !passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new InvalidProfilePasswordException();

        var newHash = passwordHasher.Hash(request.NewPassword);
        if (!await repository.ChangePasswordAsync(user.Id, user.PasswordHash, newHash,
                timeProvider.GetUtcNow(), cancellationToken))
            throw new InvalidProfilePasswordException();
    }
}

public sealed record UploadProfileAvatarCommand(Guid UserId, byte[] Content, string ContentType) : IRequest<ProfileDto>;

public sealed class UploadProfileAvatarCommandHandler(IProfileRepository repository, IAvatarStore store)
    : IRequestHandler<UploadProfileAvatarCommand, ProfileDto>
{
    public const int MaxBytes = 2 * 1024 * 1024;

    public async Task<ProfileDto> Handle(UploadProfileAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await repository.FindAsync(request.UserId, cancellationToken);
        if (user is not { IsActive: true })
            throw new InvalidSessionException();
        var extension = DetectExtension(request.Content);
        var expectedContentType = extension switch
        {
            "png" => "image/png",
            "jpg" => "image/jpeg",
            "webp" => "image/webp",
            _ => null
        };
        if (extension is null || request.Content.Length > MaxBytes || request.Content.Length == 0 ||
            request.ContentType != expectedContentType)
            throw new InvalidAvatarException();

        var key = $"avatars/{user.Id:N}/{Guid.NewGuid():N}.{extension}";
        await store.UploadAsync(key, request.Content, request.ContentType, cancellationToken);
        if (!await repository.UpdateAvatarAsync(user.Id, key, cancellationToken))
        {
            await store.DeleteAsync(key, cancellationToken);
            throw new InvalidSessionException();
        }
        if (user.AvatarObjectKey is not null)
        {
            try { await store.DeleteAsync(user.AvatarObjectKey, cancellationToken); }
            catch { /* Old object cleanup is best effort; the new avatar is already saved. */ }
        }
        var updated = await repository.FindAsync(user.Id, cancellationToken) ?? throw new InvalidSessionException();
        return ProfileDto.From(updated);
    }

    private static string? DetectExtension(byte[] bytes)
    {
        if (bytes.Length >= 45 &&
            bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }) &&
            bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) &&
            bytes.AsSpan(bytes.Length - 8, 4).SequenceEqual("IEND"u8))
            return "png";
        if (bytes.Length >= 4 && bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xd8, 0xff }) &&
            bytes.AsSpan(bytes.Length - 2).SequenceEqual(new byte[] { 0xff, 0xd9 }))
            return "jpg";
        if (bytes.Length >= 20 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) &&
            (bytes.AsSpan(12, 4).SequenceEqual("VP8 "u8) || bytes.AsSpan(12, 4).SequenceEqual("VP8L"u8) ||
             bytes.AsSpan(12, 4).SequenceEqual("VP8X"u8)))
            return "webp";
        return null;
    }
}

public sealed record DeleteProfileAvatarCommand(Guid UserId) : IRequest;

public sealed class DeleteProfileAvatarCommandHandler(IProfileRepository repository, IAvatarStore store)
    : IRequestHandler<DeleteProfileAvatarCommand>
{
    public async Task Handle(DeleteProfileAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await repository.FindAsync(request.UserId, cancellationToken);
        if (user is not { IsActive: true })
            throw new InvalidSessionException();
        var objectKey = await repository.ClearAvatarAsync(user.Id, cancellationToken);
        if (objectKey is not null)
            await store.DeleteAsync(objectKey, cancellationToken);
    }
}

public sealed class InvalidProfilePasswordException : Exception;
public sealed class InvalidAvatarException : Exception;

using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;
using TOEICMaster.Application.Authentication;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Email;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Accounts.Commands.CreateAdminUser;

public sealed record CreateAdminUserCommand(
    string FullName,
    string Email,
    string Role,
    bool SendActivationEmail) : IRequest<AdminUserDto>;

public sealed class CreateAdminUserCommandValidator : AbstractValidator<CreateAdminUserCommand>
{
    public CreateAdminUserCommandValidator()
    {
        RuleFor(command => command.FullName).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Họ tên là bắt buộc.")
            .Must(name => name.Trim().Length <= 100)
            .WithMessage("Họ tên không được vượt quá 100 ký tự.");
        RuleFor(command => command.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email là bắt buộc.")
            .Must(email => email.Trim().Length <= 254).WithMessage("Email không hợp lệ.")
            .Must(email => new EmailAddressAttribute().IsValid(email.Trim())).WithMessage("Email không hợp lệ.");
        RuleFor(command => command.Role)
            .Must(role => role is AdminUserValues.RoleUser or AdminUserValues.RoleAdmin)
            .WithMessage("Vai trò phải là USER hoặc ADMIN.");
    }
}

public sealed class CreateAdminUserCommandHandler(
    IAdminUserRepository repository,
    IUserRegistrationRepository userRegistrationRepository,
    IPasswordHasher passwordHasher,
    IEmailVerificationCodeService codeService,
    IMailSender mailSender,
    TimeProvider timeProvider) : IRequestHandler<CreateAdminUserCommand, AdminUserDto>
{
    public async Task<AdminUserDto> Handle(CreateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        var role = request.Role == AdminUserValues.RoleAdmin ? UserRole.Admin : UserRole.User;
        var nowUtc = timeProvider.GetUtcNow();

        // Admin-created local accounts do not accept a caller-supplied password, so initialize an unguessable hash.
        var initialSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var passwordHash = passwordHasher.Hash(initialSecret);

        if (request.SendActivationEmail)
        {
            var code = codeService.Issue(normalizedEmail);
            var user = User.CreateByAdmin(fullName, email, normalizedEmail, passwordHash, role,
                isActive: false, emailVerifiedAtUtc: null, createdAtUtc: nowUtc);
            var challenge = EmailVerificationChallenge.Create(user.Id, code.CodeHash, nowUtc,
                nowUtc + codeService.Lifetime);

            if (!await repository.TryCreateAsync(user, challenge, cancellationToken))
                throw new DuplicateEmailException();

            await SendActivationAsync(user.Id, email, code.Code, nowUtc, cancellationToken);
            return ToDto(user, request.Role);
        }

        var activeUser = User.CreateByAdmin(fullName, email, normalizedEmail, passwordHash, role,
            isActive: true, emailVerifiedAtUtc: nowUtc, createdAtUtc: nowUtc);

        if (!await repository.TryCreateAsync(activeUser, challenge: null, cancellationToken))
            throw new DuplicateEmailException();

        return ToDto(activeUser, request.Role);
    }

    private async Task SendActivationAsync(Guid userId, string email, string code, DateTimeOffset sentAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await mailSender.SendAsync(EmailVerificationMail.Create(email, code, codeService.Lifetime),
                cancellationToken);
        }
        catch
        {
            try
            {
                await userRegistrationRepository.ReleaseVerificationCooldownAsync(userId, sentAtUtc,
                    codeService.ResendCooldown, CancellationToken.None);
            }
            catch
            {
                // Preserve the mail delivery exception; cooldown release is a best-effort retry aid.
            }

            throw;
        }
    }

    private static AdminUserDto ToDto(User user, string role) => new(
        user.Id,
        user.DisplayName,
        user.Email,
        AvatarUrl: null,
        role,
        user.IsActive ? AdminUserValues.StatusActive : AdminUserValues.StatusPending,
        user.EmailVerifiedAtUtc is not null,
        AdminUserValues.ProviderPassword,
        user.CreatedAtUtc,
        LastLoginAt: null);
}

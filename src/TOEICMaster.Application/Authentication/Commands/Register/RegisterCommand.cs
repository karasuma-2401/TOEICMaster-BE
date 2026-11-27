using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Authentication.Commands.Register;

public sealed record RegisterCommand(string DisplayName, string Email, string Password) : IRequest;

public sealed class RegisterCommandHandler(
    IUserRegistrationRepository userRepository,
    IPasswordHasher passwordHasher,
    IEmailVerificationCodeService codeService,
    TimeProvider timeProvider) : IRequestHandler<RegisterCommand>
{
    public async Task Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName.Trim();
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        var existing = await userRepository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsActive || existing.IsEmailVerified)
                throw new DuplicateEmailException();
            await ReplacePendingAsync(existing, displayName, normalizedEmail, request.Password, cancellationToken);
            return;
        }

        var nowUtc = timeProvider.GetUtcNow();
        var code = codeService.Issue(normalizedEmail);
        var passwordHash = passwordHasher.Hash(request.Password);
        var user = User.CreateLocal(displayName, email, normalizedEmail, passwordHash: null, nowUtc);
        var challenge = EmailVerificationChallenge.Create(user.Id, code.CodeHash, nowUtc,
            nowUtc + codeService.Lifetime, displayName, passwordHash);
        var mail = EmailVerificationMail.Create(email, code.Code, codeService.Lifetime);

        if (!await userRepository.TryAddAsync(user, challenge, mail, cancellationToken))
        {
            existing = await userRepository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);
            if (existing is null || existing.IsActive || existing.IsEmailVerified)
                throw new DuplicateEmailException();
            await ReplacePendingAsync(existing, displayName, normalizedEmail, request.Password, cancellationToken);
        }
    }

    private async Task ReplacePendingAsync(RegistrationAccount account, string displayName, string normalizedEmail,
        string password, CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var code = codeService.Issue(normalizedEmail);
        var passwordHash = passwordHasher.Hash(password);
        var mail = EmailVerificationMail.Create(account.Email, code.Code, codeService.Lifetime);

        if (!await userRepository.TryReplacePendingRegistrationAsync(account.UserId, code.CodeHash, displayName,
                passwordHash, nowUtc, nowUtc + codeService.Lifetime, mail, cancellationToken))
        {
            var refreshed = await userRepository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);
            if (refreshed is null || refreshed.IsActive || refreshed.IsEmailVerified)
                throw new DuplicateEmailException();
        }
    }
}

using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Email;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Authentication.Commands.Register;

public sealed record RegisterCommand(string DisplayName, string Email, string Password) : IRequest;

public sealed class RegisterCommandHandler(
    IUserRegistrationRepository userRepository,
    IPasswordHasher passwordHasher,
    IEmailVerificationCodeService codeService,
    IMailSender mailSender,
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
            await IssueForExistingAsync(existing, normalizedEmail, cancellationToken);
            return;
        }

        var nowUtc = timeProvider.GetUtcNow();
        var code = codeService.Issue(normalizedEmail);
        var user = User.CreateLocal(displayName, email, normalizedEmail, passwordHasher.Hash(request.Password),
            nowUtc);
        var challenge = EmailVerificationChallenge.Create(user.Id, code.CodeHash, nowUtc,
            nowUtc + codeService.Lifetime);
        if (!await userRepository.TryAddAsync(user, challenge, cancellationToken))
        {
            existing = await userRepository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);
            if (existing is null || existing.IsActive || existing.IsEmailVerified)
                throw new DuplicateEmailException();
            await IssueForExistingAsync(existing, normalizedEmail, cancellationToken);
            return;
        }

        await SendAsync(user.Id, email, code.Code, nowUtc, cancellationToken);
    }

    private async Task IssueForExistingAsync(RegistrationAccount account, string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var code = codeService.Issue(normalizedEmail);
        if (!await userRepository.TryReplaceVerificationAsync(account.UserId, code.CodeHash, nowUtc,
                nowUtc + codeService.Lifetime, codeService.ResendCooldown, cancellationToken))
            return;
        await SendAsync(account.UserId, account.Email, code.Code, nowUtc, cancellationToken);
    }

    private async Task SendAsync(Guid userId, string email, string code, DateTimeOffset sentAtUtc,
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
                await userRepository.ReleaseVerificationCooldownAsync(userId, sentAtUtc,
                    codeService.ResendCooldown, CancellationToken.None);
            }
            catch
            {
                // Preserve the mail delivery exception; cooldown release is a best-effort retry aid.
            }
            throw;
        }
    }
}

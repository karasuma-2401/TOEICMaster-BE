using MediatR;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Email;

namespace TOEICMaster.Application.Authentication.Commands.ResendEmailVerification;

public sealed record ResendEmailVerificationCommand(string Email) : IRequest;

public sealed class ResendEmailVerificationCommandHandler(
    IUserRegistrationRepository repository,
    IEmailVerificationCodeService codeService,
    IMailSender mailSender,
    TimeProvider timeProvider) : IRequestHandler<ResendEmailVerificationCommand>
{
    public async Task Handle(ResendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var account = await repository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        if (account is null || account.IsActive || account.IsEmailVerified)
            return;

        var nowUtc = timeProvider.GetUtcNow();
        var code = codeService.Issue(normalizedEmail);
        if (!await repository.TryReplaceVerificationAsync(account.UserId, code.CodeHash, nowUtc,
                nowUtc + codeService.Lifetime, codeService.ResendCooldown, cancellationToken))
            return;

        try
        {
            await mailSender.SendAsync(EmailVerificationMail.Create(account.Email, code.Code, codeService.Lifetime),
                cancellationToken);
        }
        catch
        {
            try
            {
                await repository.ReleaseVerificationCooldownAsync(account.UserId, nowUtc,
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

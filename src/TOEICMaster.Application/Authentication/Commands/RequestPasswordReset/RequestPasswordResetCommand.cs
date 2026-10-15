using MediatR;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Email;

namespace TOEICMaster.Application.Authentication.Commands.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : IRequest;

public sealed class RequestPasswordResetCommandHandler(
    IPasswordResetRepository repository,
    IEmailVerificationCodeService codeService,
    IMailSender mailSender,
    TimeProvider timeProvider) : IRequestHandler<RequestPasswordResetCommand>
{
    public async Task Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var account = await repository.FindEligibleAccountAsync(normalizedEmail, cancellationToken);
        if (account is null)
            return;

        var nowUtc = timeProvider.GetUtcNow();
        var code = codeService.Issue(normalizedEmail);
        if (!await repository.TryReplaceChallengeAsync(account.UserId, code.CodeHash, nowUtc,
                nowUtc + codeService.Lifetime, codeService.ResendCooldown, cancellationToken))
            return;

        try
        {
            await mailSender.SendAsync(PasswordResetMail.Create(account.Email, code.Code, codeService.Lifetime),
                cancellationToken);
        }
        catch
        {
            try
            {
                await repository.ReleaseChallengeCooldownAsync(account.UserId, nowUtc,
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

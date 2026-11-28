using MediatR;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Application.Authentication.Commands.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : IRequest;

public sealed class RequestPasswordResetCommandHandler(
    IPasswordResetRepository repository,
    IEmailVerificationCodeService codeService,
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
        var mail = PasswordResetMail.Create(account.Email, code.Code, codeService.Lifetime);
        await repository.TryReplaceChallengeWithEmailAsync(account.UserId, code.CodeHash, nowUtc,
            nowUtc + codeService.Lifetime, codeService.ResendCooldown, mail, cancellationToken);
    }
}

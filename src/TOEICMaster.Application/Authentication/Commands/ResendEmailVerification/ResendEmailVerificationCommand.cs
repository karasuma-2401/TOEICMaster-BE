using MediatR;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Application.Authentication.Commands.ResendEmailVerification;

public sealed record ResendEmailVerificationCommand(string Email) : IRequest;

public sealed class ResendEmailVerificationCommandHandler(
    IUserRegistrationRepository repository,
    IEmailVerificationCodeService codeService,
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
        var mail = EmailVerificationMail.Create(account.Email, code.Code, codeService.Lifetime);
        await repository.TryReplaceVerificationAsync(account.UserId, code.CodeHash, nowUtc,
            nowUtc + codeService.Lifetime, codeService.ResendCooldown, mail, cancellationToken);
    }
}

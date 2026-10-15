using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Application.Authentication.Commands.VerifyPasswordReset;

public sealed record VerifyPasswordResetCommand(string Email, string Code) : IRequest<PasswordResetVerificationResult>;

public sealed record PasswordResetVerificationResult(string ResetToken, DateTimeOffset ExpiresAtUtc);

public sealed class VerifyPasswordResetCommandHandler(
    IPasswordResetRepository repository,
    IEmailVerificationCodeService codeService,
    IPasswordResetTokenService tokenService,
    TimeProvider timeProvider) : IRequestHandler<VerifyPasswordResetCommand, PasswordResetVerificationResult>
{
    public async Task<PasswordResetVerificationResult> Handle(VerifyPasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var codeHash = codeService.Hash(normalizedEmail, request.Code);
        var token = tokenService.Issue();
        var nowUtc = timeProvider.GetUtcNow();
        var expiresAtUtc = nowUtc + tokenService.Lifetime;
        var result = await repository.VerifyCodeAsync(normalizedEmail, codeHash, nowUtc,
            codeService.MaxFailedAttempts, token.TokenHash, expiresAtUtc, cancellationToken);
        if (result != PasswordResetCodeAttempt.Verified)
            throw new InvalidPasswordResetCodeException();

        return new PasswordResetVerificationResult(token.Token, expiresAtUtc);
    }
}

using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Application.Authentication.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(string Email, string Code) : IRequest;

public sealed class VerifyEmailCommandHandler(
    IUserRegistrationRepository repository,
    IEmailVerificationCodeService codeService,
    TimeProvider timeProvider) : IRequestHandler<VerifyEmailCommand>
{
    public async Task Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var codeHash = codeService.Hash(normalizedEmail, request.Code);
        var result = await repository.VerifyEmailAsync(normalizedEmail, codeHash, timeProvider.GetUtcNow(),
            codeService.MaxFailedAttempts, cancellationToken);
        if (result != EmailVerificationAttempt.Verified)
            throw new InvalidEmailVerificationException();
    }
}

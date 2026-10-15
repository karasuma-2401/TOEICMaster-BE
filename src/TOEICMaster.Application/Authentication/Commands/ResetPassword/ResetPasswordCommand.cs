using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Application.Authentication.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string ResetToken, string NewPassword) : IRequest;

public sealed class ResetPasswordCommandHandler(
    IPasswordResetRepository repository,
    IPasswordResetTokenService tokenService,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.Hash(request.ResetToken);
        var passwordHash = passwordHasher.Hash(request.NewPassword);
        if (!await repository.ResetPasswordAsync(tokenHash, passwordHash, timeProvider.GetUtcNow(),
                cancellationToken))
            throw new InvalidPasswordResetTokenException();
    }
}

using MediatR;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Application.Authentication.Commands.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest;

public sealed class LogoutCommandHandler(
    IAuthenticationRepository authenticationRepository,
    IAuthenticationTokenService tokenService,
    TimeProvider timeProvider) : IRequestHandler<LogoutCommand>
{
    public Task Handle(LogoutCommand request, CancellationToken cancellationToken) =>
        authenticationRepository.RevokeSessionAsync(tokenService.HashRefreshToken(request.RefreshToken),
            timeProvider.GetUtcNow(), cancellationToken);
}

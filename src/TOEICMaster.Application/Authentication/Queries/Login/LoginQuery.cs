using MediatR;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;

namespace TOEICMaster.Application.Authentication.Queries.Login;

public sealed record LoginQuery(string Email, string Password) : IRequest<LoginResult>;

public sealed record LoginResult(string AccessToken, Guid UserId, string Email, string FullName);

public sealed class LoginQueryHandler(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService) : IRequestHandler<LoginQuery, LoginResult>
{
    public async Task<LoginResult> Handle(LoginQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        await Task.Yield();
        if (user is null || !passwordHasher.VerifyHashedPassword(user.PasswordHash, request.Password) || !user.IsActive)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var roles = await userRepository.GetRolesAsync(user.Id, cancellationToken);
        var token = tokenService.GenerateToken(user.Id, user.Email, roles);

        return new LoginResult(token, user.Id, user.Email, user.FullName);
    }
}

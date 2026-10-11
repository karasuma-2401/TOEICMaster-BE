using MediatR;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Authentication.Commands.Register;

public sealed record RegisterCommand(string DisplayName, string Email, string Password) : IRequest;

public sealed class RegisterCommandHandler(
    IUserRegistrationRepository userRepository,
    IPasswordHasher passwordHasher) : IRequestHandler<RegisterCommand>
{
    public async Task Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName.Trim();
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        if (await userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
            throw new DuplicateEmailException();

        var user = User.CreateLocal(displayName, email, normalizedEmail, passwordHasher.Hash(request.Password),
            DateTimeOffset.UtcNow);
        if (!await userRepository.TryAddAsync(user, cancellationToken))
            throw new DuplicateEmailException();
    }
}

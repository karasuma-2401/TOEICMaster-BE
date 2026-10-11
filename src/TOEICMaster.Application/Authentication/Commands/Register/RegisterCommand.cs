using MediatR;
using TOEICMaster.Application.Common.Interfaces;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Authentication.Commands.Register;

public sealed record RegisterCommand(string Email, string Password, string FullName) : IRequest<Guid>;

public sealed class RegisterCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher) : IRequestHandler<RegisterCommand, Guid>
{
    public async Task<Guid> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existing = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
            throw new FluentValidation.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Email", "Email already in use.") });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            FullName = request.FullName,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            PasswordHash = passwordHasher.HashPassword(request.Password)
        };

        await userRepository.AddAsync(user, cancellationToken);

        // Assign default "User" role if exists
        var roleId = await userRepository.GetRoleIdByNameAsync("User", cancellationToken);
        if (roleId.HasValue)
        {
            await userRepository.AssignRoleAsync(user.Id, roleId.Value, cancellationToken);
        }

        return user.Id;
    }
}

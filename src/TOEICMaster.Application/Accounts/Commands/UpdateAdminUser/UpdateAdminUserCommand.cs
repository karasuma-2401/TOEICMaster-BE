using FluentValidation;
using MediatR;
using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Accounts.Commands.UpdateAdminUser;

public sealed record UpdateAdminUserCommand(Guid ActorUserId, Guid TargetUserId, string Role) : IRequest<AdminUserDto>;

public sealed class UpdateAdminUserCommandValidator : AbstractValidator<UpdateAdminUserCommand>
{
    public UpdateAdminUserCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.TargetUserId).NotEmpty();
        RuleFor(command => command.Role)
            .Must(role => role is AdminUserValues.RoleUser or AdminUserValues.RoleAdmin)
            .WithMessage("Role must be USER or ADMIN.");
    }
}

public sealed class UpdateAdminUserCommandHandler(IAdminUserRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdateAdminUserCommand, AdminUserDto>
{
    public Task<AdminUserDto> Handle(UpdateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var role = request.Role == AdminUserValues.RoleAdmin ? UserRole.Admin : UserRole.User;
        return repository.UpdateRoleAsync(request.ActorUserId, request.TargetUserId, role, timeProvider.GetUtcNow(),
            cancellationToken);
    }
}

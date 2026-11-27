using FluentValidation;
using MediatR;

namespace TOEICMaster.Application.Accounts.Commands.UnlockAdminUser;

public sealed record UnlockAdminUserCommand(Guid ActorUserId, Guid TargetUserId) : IRequest;

public sealed class UnlockAdminUserCommandValidator : AbstractValidator<UnlockAdminUserCommand>
{
    public UnlockAdminUserCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.TargetUserId).NotEmpty();
    }
}

public sealed class UnlockAdminUserCommandHandler(IAdminUserRepository repository)
    : IRequestHandler<UnlockAdminUserCommand>
{
    public Task Handle(UnlockAdminUserCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == request.TargetUserId)
            throw new AdminUserOperationConflictException("CANNOT_UNLOCK_SELF", "Cannot unlock your own account.");
        return repository.UnlockAsync(request.TargetUserId, cancellationToken);
    }
}

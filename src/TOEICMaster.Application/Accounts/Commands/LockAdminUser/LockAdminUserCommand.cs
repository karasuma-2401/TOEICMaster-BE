using FluentValidation;
using MediatR;

namespace TOEICMaster.Application.Accounts.Commands.LockAdminUser;

public sealed record LockAdminUserCommand(Guid ActorUserId, Guid TargetUserId) : IRequest;

public sealed class LockAdminUserCommandValidator : AbstractValidator<LockAdminUserCommand>
{
    public LockAdminUserCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.TargetUserId).NotEmpty();
    }
}

public sealed class LockAdminUserCommandHandler(IAdminUserRepository repository, TimeProvider timeProvider)
    : IRequestHandler<LockAdminUserCommand>
{
    public Task Handle(LockAdminUserCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == request.TargetUserId)
            throw new AdminUserOperationConflictException("CANNOT_LOCK_SELF",
                "Cannot lock your own admin account.");

        return repository.LockAsync(request.ActorUserId, request.TargetUserId, timeProvider.GetUtcNow(),
            cancellationToken);
    }
}

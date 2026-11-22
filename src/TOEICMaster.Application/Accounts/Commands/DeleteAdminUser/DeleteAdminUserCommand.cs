using FluentValidation;
using MediatR;
using TOEICMaster.Application.Profile;

namespace TOEICMaster.Application.Accounts.Commands.DeleteAdminUser;

public sealed record DeleteAdminUserCommand(Guid ActorUserId, Guid TargetUserId) : IRequest;

public sealed class DeleteAdminUserCommandValidator : AbstractValidator<DeleteAdminUserCommand>
{
    public DeleteAdminUserCommandValidator()
    {
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.TargetUserId).NotEmpty();
    }
}

public sealed class DeleteAdminUserCommandHandler(IAdminUserRepository repository, IAvatarStore avatarStore)
    : IRequestHandler<DeleteAdminUserCommand>
{
    public async Task Handle(DeleteAdminUserCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == request.TargetUserId)
            throw new AdminUserOperationConflictException("CANNOT_DELETE_SELF",
                "Không thể tự xóa tài khoản quản trị của chính mình.");

        var avatarObjectKey = await repository.DeleteAsync(request.ActorUserId, request.TargetUserId, cancellationToken);
        if (avatarObjectKey is not null)
        {
            try
            {
                await avatarStore.DeleteAsync(avatarObjectKey, CancellationToken.None);
            }
            catch
            {
                // Best-effort storage cleanup after the user deletion transaction has already committed.
            }
        }
    }
}

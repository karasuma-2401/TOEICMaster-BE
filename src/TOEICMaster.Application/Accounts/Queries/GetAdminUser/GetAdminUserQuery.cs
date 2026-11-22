using MediatR;
using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;

namespace TOEICMaster.Application.Accounts.Queries.GetAdminUser;

public sealed record GetAdminUserQuery(Guid UserId) : IRequest<AdminUserDto>;

public sealed class GetAdminUserQueryHandler(IAdminUserRepository repository)
    : IRequestHandler<GetAdminUserQuery, AdminUserDto>
{
    public async Task<AdminUserDto> Handle(GetAdminUserQuery request, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(request.UserId, cancellationToken)
        ?? throw new AdminUserNotFoundException();
}

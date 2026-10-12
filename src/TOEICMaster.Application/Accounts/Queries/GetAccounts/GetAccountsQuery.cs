using MediatR;

namespace TOEICMaster.Application.Accounts.Queries.GetAccounts;

public sealed record GetAccountsQuery(int PageNumber = 1, int PageSize = 20) : IRequest<AccountPageDto>;

public sealed record AccountSummaryDto(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles,
    bool IsActive, DateTimeOffset CreatedAt);

public sealed record AccountPageDto(IReadOnlyList<AccountSummaryDto> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)(((long)TotalCount + PageSize - 1) / PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}

public sealed class GetAccountsQueryHandler(IAccountReader reader) : IRequestHandler<GetAccountsQuery, AccountPageDto>
{
    public Task<AccountPageDto> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
        => reader.GetPageAsync(request.PageNumber, request.PageSize, cancellationToken);
}

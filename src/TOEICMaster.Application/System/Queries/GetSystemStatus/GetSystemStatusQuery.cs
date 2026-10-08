using MediatR;

namespace TOEICMaster.Application.System.Queries.GetSystemStatus;

public sealed record GetSystemStatusQuery : IRequest<SystemStatusDto>;

public sealed record SystemStatusDto(string Service, string Status);

public sealed class GetSystemStatusQueryHandler : IRequestHandler<GetSystemStatusQuery, SystemStatusDto>
{
    public Task<SystemStatusDto> Handle(GetSystemStatusQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new SystemStatusDto("TOEICMaster", "ok"));
}

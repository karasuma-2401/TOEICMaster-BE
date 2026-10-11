using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.System.Queries.GetSystemStatus;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(ISender sender, IMapper mapper) : ControllerBase
{
    [HttpGet("ping")]
    public async Task<ActionResult<PingResponse>> Ping(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSystemStatusQuery(), cancellationToken);
        return Ok(mapper.Map<PingResponse>(result));
    }
}

public sealed record PingResponse(string Service, string Status);

using MediatR;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.System.Queries.GetSystemStatus;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemController(ISender sender) : ControllerBase
{
    [HttpGet("ping")]
    public async Task<ActionResult<PingResponse>> Ping(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSystemStatusQuery(), cancellationToken);
        return Ok(new PingResponse(result.Service, result.Status));
    }
}

public sealed record PingResponse(string Service, string Status);

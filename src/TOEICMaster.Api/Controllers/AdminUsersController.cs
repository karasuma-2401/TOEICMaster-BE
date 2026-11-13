using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = "AccountAdministration")]
public sealed class AdminUsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminUserPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminUserPageDto>> GetUsers(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? q = null,
        [FromQuery] string? role = null, [FromQuery] string? status = null,
        [FromQuery] string sort = "createdAt:desc", CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetAdminUsersQuery(page, pageSize, q, role, status, sort), cancellationToken);
        return Ok(result);
    }
}

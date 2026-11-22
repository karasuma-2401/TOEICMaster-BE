using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Api.Extensions;
using TOEICMaster.Application.Accounts.Commands.CreateAdminUser;
using TOEICMaster.Application.Accounts.Commands.DeleteAdminUser;
using TOEICMaster.Application.Accounts.Commands.LockAdminUser;
using TOEICMaster.Application.Accounts.Commands.ResetAdminUserPassword;
using TOEICMaster.Application.Accounts.Commands.UnlockAdminUser;
using TOEICMaster.Application.Accounts.Commands.UpdateAdminUser;
using TOEICMaster.Application.Accounts.Queries.GetAdminUser;
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

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDto>> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAdminUserQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserDto>> Create([FromBody] CreateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(
            new CreateAdminUserCommand(request.FullName, request.Email, request.Role, request.SendActivationEmail),
            cancellationToken);
        return CreatedAtAction(nameof(GetUser), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserDto>> Update(Guid id, [FromBody] UpdateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await sender.Send(
            new UpdateAdminUserCommand(User.GetRequiredUserId(), id, request.Role),
            cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteAdminUserCommand(User.GetRequiredUserId(), id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Lock(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new LockAdminUserCommand(User.GetRequiredUserId(), id), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new UnlockAdminUserCommand(User.GetRequiredUserId(), id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new ResetAdminUserPasswordCommand(User.GetRequiredUserId(), id), cancellationToken);
        return NoContent();
    }
}

public sealed record CreateAdminUserRequest(
    string FullName,
    string Email,
    string Role,
    bool SendActivationEmail = true);

public sealed record UpdateAdminUserRequest(string Role);

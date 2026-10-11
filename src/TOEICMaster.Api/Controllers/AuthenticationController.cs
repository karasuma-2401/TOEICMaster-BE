using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using TOEICMaster.Application.Authentication.Commands.Register;
using TOEICMaster.Application.Authentication.Queries.Login;

namespace TOEICMaster.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class AuthenticationController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthenticationController(IMediator mediator) => _mediator = mediator;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterCommand command)
    {
        try
        {
            var id = await _mediator.Send(command);
            return Created(string.Empty, new { id });
        }
        catch (ValidationException vex)
        {
            var errors = vex.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
            return ValidationProblem(new ValidationProblemDetails(errors));
        }
        catch (TOEICMaster.Application.Common.Exceptions.DuplicateEmailException)
        {
            return Conflict(new { error = "Email already in use." });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginQuery query)
    {
        try
        {
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Ok(new { id, email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin-only")]
    public IActionResult AdminOnly() => Ok(new { message = "admin" });
}

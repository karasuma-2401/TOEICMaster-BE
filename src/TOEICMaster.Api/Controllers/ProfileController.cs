using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using TOEICMaster.Api.Configuration;
using TOEICMaster.Application.Profile;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/profile")]
[Route("api/v1/users/me")]
public sealed class ProfileController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetProfileQuery(CurrentUserId()), cancellationToken));

    [HttpPatch]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileDto>> UpdateName(UpdateProfileNameRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateProfileNameCommand(CurrentUserId(), request.DisplayName), cancellationToken));

    [HttpPut]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileDto>> Update(UpdateProfileRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateProfileCommand(CurrentUserId(), request.DisplayName, request.Phone,
            request.DateOfBirth, request.TargetScore), cancellationToken));

    [HttpPut("password")]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangeProfilePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ChangeProfilePasswordCommand(CurrentUserId(), request.CurrentPassword,
            request.NewPassword), cancellationToken);
        return NoContent();
    }

    [HttpPost("password")]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePasswordForFrontend(ChangeProfilePasswordFrontendRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.ConfirmPassword) || request.NewPassword != request.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(request.ConfirmPassword), "Mật khẩu xác nhận chưa khớp.");
            return ValidationProblem(ModelState);
        }
        await sender.Send(new ChangeProfilePasswordCommand(CurrentUserId(), request.CurrentPassword,
            request.NewPassword), cancellationToken);
        return NoContent();
    }

    [HttpPut("avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024 + 64 * 1024)]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileDto>> UploadAvatar([FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > UploadProfileAvatarCommandHandler.MaxBytes)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Ảnh đại diện không hợp lệ." });
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var profile = await sender.Send(
            new UploadProfileAvatarCommand(CurrentUserId(), stream.ToArray(), file.ContentType), cancellationToken);
        return Ok(profile);
    }

    [HttpGet("avatar")]
    public async Task<IActionResult> GetAvatar(CancellationToken cancellationToken)
    {
        var avatar = await sender.Send(new GetProfileAvatarQuery(CurrentUserId()), cancellationToken);
        if (avatar is null)
            return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(avatar.Data, avatar.ContentType);
    }

    [HttpDelete("avatar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAvatar(CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteProfileAvatarCommand(CurrentUserId()), cancellationToken);
        return NoContent();
    }

    private Guid CurrentUserId()
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var userId))
            throw new UnauthorizedAccessException();
        return userId;
    }
}

public sealed record UpdateProfileNameRequest(string DisplayName);
public sealed record UpdateProfileRequest(string DisplayName, string? Phone, DateOnly? DateOfBirth, int? TargetScore);
public sealed record ChangeProfilePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ChangeProfilePasswordFrontendRequest(
    string CurrentPassword, string NewPassword, string ConfirmPassword);

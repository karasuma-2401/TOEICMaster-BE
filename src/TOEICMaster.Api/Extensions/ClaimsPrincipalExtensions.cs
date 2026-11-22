using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace TOEICMaster.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal user) =>
        user.TryGetUserId() ?? throw new UnauthorizedAccessException("User identifier not found in claims.");

    public static Guid? TryGetUserId(this ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }
}

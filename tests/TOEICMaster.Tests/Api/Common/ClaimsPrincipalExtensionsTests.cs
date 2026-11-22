using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using TOEICMaster.Api.Extensions;

namespace TOEICMaster.Tests.Api.Common;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void TryGetUserId_extracts_guid_from_sub_or_name_identifier_with_sub_precedence()
    {
        var subId = Guid.NewGuid();
        var nameId = Guid.NewGuid();

        var subOnly = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, subId.ToString())]));
        Assert.Equal(subId, subOnly.TryGetUserId());
        Assert.Equal(subId, subOnly.GetRequiredUserId());

        var nameIdOnly = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, nameId.ToString())]));
        Assert.Equal(nameId, nameIdOnly.TryGetUserId());
        Assert.Equal(nameId, nameIdOnly.GetRequiredUserId());

        var both = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, nameId.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, subId.ToString())
        ]));
        Assert.Equal(subId, both.TryGetUserId());
    }

    [Fact]
    public void Missing_or_invalid_user_id_returns_null_and_throws_from_required()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Null(anonymous.TryGetUserId());
        Assert.Throws<UnauthorizedAccessException>(() => anonymous.GetRequiredUserId());

        var malformed = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid")]));
        Assert.Null(malformed.TryGetUserId());
        Assert.Throws<UnauthorizedAccessException>(() => malformed.GetRequiredUserId());
    }
}

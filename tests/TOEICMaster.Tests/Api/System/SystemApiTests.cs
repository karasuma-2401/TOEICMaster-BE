using System.Net;
using System.Net.Http.Json;
using TOEICMaster.Api.Controllers;
using TOEICMaster.Tests.Api.Common;

namespace TOEICMaster.Tests.Api.System;

public sealed class SystemApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Scalar_api_reference_is_available_in_development()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/scalar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Liveness_does_not_require_external_services()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Responses_include_browser_protection_headers()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/live");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task Ping_has_the_frontend_contract()
    {
        using var client = factory.CreateClient();
        var response = await client.GetFromJsonAsync<PingResponse>("/api/v1/system/ping");
        Assert.Equal(new PingResponse("TOEICMaster", "ok"), response);
    }

    [Theory]
    [InlineData("http://localhost:3000", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task Cors_allows_only_configured_origins(string origin, bool allowed)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/system/ping");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}

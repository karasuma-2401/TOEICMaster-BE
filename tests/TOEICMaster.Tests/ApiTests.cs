using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using TOEICMaster.Api.Controllers;

namespace TOEICMaster.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Database=test;Username=test;Password=test;Timeout=1",
                ["ConnectionStrings:Redis"] = "127.0.0.1:1,abortConnect=false,connectTimeout=100,syncTimeout=100",
                ["Minio:Endpoint"] = "127.0.0.1:1",
                ["Minio:AccessKey"] = "test-access",
                ["Minio:SecretKey"] = "test-secret",
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
                ["Smtp:Host"] = "127.0.0.1",
                ["Smtp:Port"] = "1"
            }));
    }
}

public sealed class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Liveness_does_not_require_external_services()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

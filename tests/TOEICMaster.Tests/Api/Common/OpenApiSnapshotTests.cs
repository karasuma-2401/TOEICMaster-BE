using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TOEICMaster.Tests.Api.Common;

public sealed class OpenApiSnapshotFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Startup:SkipSeeders"] = "true",
                ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Port=5432;Database=openapi_unused;Username=postgres;Password=postgres",
                ["ConnectionStrings:Redis"] = "127.0.0.1:1,abortConnect=false,connectTimeout=100,syncTimeout=100",
                ["Minio:Endpoint"] = "127.0.0.1:1",
                ["Minio:PublicEndpoint"] = "localhost:9000",
                ["Minio:AccessKey"] = "test-access",
                ["Minio:SecretKey"] = "test-secret",
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
                ["Smtp:Host"] = "127.0.0.1",
                ["Smtp:Port"] = "1",
                ["Smtp:FromAddress"] = "noreply@toeicmaster.test",
                ["Smtp:Security"] = "None",
                ["Jwt:Issuer"] = "TOEICMaster.Tests",
                ["Jwt:Audience"] = "TOEICMaster.Tests.Client",
                ["Jwt:SigningKey"] = "test-only-signing-key-with-32-characters",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "1",
                ["Jwt:RememberMeRefreshTokenDays"] = "30",
                ["Google:ClientId"] = "test-client.apps.googleusercontent.com",
                ["EmailVerification:HashKey"] = "test-email-verification-key-32-characters"
            }));
        builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
    }
}

public sealed class OpenApiSnapshotTests
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string SnapshotFilePath()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TOEICMaster.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        return Path.Combine(root.FullName, "docs", "contracts", "openapi.json");
    }

    private static string CanonicalizeJson(string rawJson)
    {
        var node = JsonNode.Parse(rawJson)
            ?? throw new InvalidOperationException("OpenAPI endpoint returned null JSON.");
        var formatted = node.ToJsonString(CanonicalJsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        return formatted.EndsWith('\n') ? formatted : formatted + "\n";
    }

    [Fact]
    public async Task OpenApi_v1_snapshot_matches_live_endpoint_and_includes_admin_users_and_string_enums()
    {
        using var factory = new OpenApiSnapshotFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        var canonicalJson = CanonicalizeJson(rawJson);
        var snapshotPath = SnapshotFilePath();

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            await File.WriteAllTextAsync(snapshotPath, canonicalJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        Assert.True(File.Exists(snapshotPath),
            $"OpenAPI snapshot missing at {snapshotPath}. Run UPDATE_OPENAPI_SNAPSHOT=1 to generate it.");

        var expected = (await File.ReadAllTextAsync(snapshotPath, Encoding.UTF8))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(expected, canonicalJson);

        using var doc = JsonDocument.Parse(canonicalJson);
        var paths = doc.RootElement.GetProperty("paths");

        // All 8 AdminUsers operations exist
        var collectionPath = paths.GetProperty("/api/v1/admin/users");
        Assert.True(collectionPath.TryGetProperty("get", out _));
        Assert.True(collectionPath.TryGetProperty("post", out _));

        var itemPath = paths.GetProperty("/api/v1/admin/users/{id}");
        Assert.True(itemPath.TryGetProperty("get", out _));
        Assert.True(itemPath.TryGetProperty("patch", out _));
        Assert.True(itemPath.TryGetProperty("delete", out _));

        var lockPath = paths.GetProperty("/api/v1/admin/users/{id}/lock");
        Assert.True(lockPath.TryGetProperty("post", out _));
        Assert.True(lockPath.TryGetProperty("delete", out _));

        var resetPasswordPath = paths.GetProperty("/api/v1/admin/users/{id}/reset-password");
        Assert.True(resetPasswordPath.TryGetProperty("post", out _));

        // Enum schemas reflect string values matching runtime JSON contract
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        var examRevisionStatusValues = schemas.GetProperty("ExamRevisionStatus")
            .GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("Draft", examRevisionStatusValues);
        Assert.Contains("Published", examRevisionStatusValues);
    }
}

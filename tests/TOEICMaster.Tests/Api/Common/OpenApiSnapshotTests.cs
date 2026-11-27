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
    [Fact]
    public async Task Knowledge_responses_and_admin_learner_context_schemas_are_distinct()
    {
        using var factory = new OpenApiSnapshotFactory();
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var media = root.GetProperty("paths").GetProperty("/api/v1/knowledge/media/{assetId}")
            .GetProperty("get").GetProperty("responses").GetProperty("200");
        Assert.DoesNotContain("FileContentResult", media.GetRawText());
        var saved = root.GetProperty("paths").GetProperty("/api/v1/knowledge/saved/{articleId}");
        foreach (var method in new[] { "put", "delete" })
        {
            var responses = saved.GetProperty(method).GetProperty("responses");
            Assert.True(responses.TryGetProperty("204", out _));
            Assert.False(responses.TryGetProperty("200", out _));
        }
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var admin = schemas.GetProperty("AdminExamContextBlockDto").GetProperty("properties");
        var learner = schemas.GetProperty("ExamAttemptContextBlockDto").GetProperty("properties");
        Assert.True(admin.TryGetProperty("imageRequired", out _));
        Assert.False(learner.TryGetProperty("imageRequired", out _));
        Assert.True(learner.TryGetProperty("id", out _));
        Assert.False(admin.TryGetProperty("id", out _));
    }

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
    public async Task Writing_import_has_a_named_response_schema_with_integer_importedCount()
    {
        using var factory = new OpenApiSnapshotFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schema = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/admin/writing/exercises/import").GetProperty("post")
            .GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        Assert.Equal("#/components/schemas/ImportWritingItemsResponse", schema.GetProperty("$ref").GetString());
        var count = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("ImportWritingItemsResponse").GetProperty("properties").GetProperty("importedCount");
        var type = count.GetProperty("type");
        var types = type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(value => value.GetString()).ToArray()
            : [type.GetString()];
        Assert.Contains("integer", types);
        Assert.DoesNotContain("null", types);
        Assert.Equal("int32", count.GetProperty("format").GetString());
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

        var examBlockTypeValues = schemas.GetProperty("ExamBlockType")
            .GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(new[] { "Single", "Group" }, examBlockTypeValues);

        var examContextTypeValues = schemas.GetProperty("ExamContextType")
            .GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(new[] { "Text", "Image" }, examContextTypeValues);

        var examAttemptStatusValues = schemas.GetProperty("ExamAttemptStatus")
            .GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(new[] { "InProgress", "Completed", "Abandoned" }, examAttemptStatusValues);

        // File upload operations emit multipart/form-data with binary string schema, never exploded IFormFile metadata.
        (string Path, string Method, string FileField)[] uploadOperations =
        [
            ("/api/v1/admin/exam-media/images", "post", "file"),
            ("/api/v1/admin/exams/import", "post", "workbook"),
            ("/api/v1/admin/knowledge/media/images", "post", "file"),
            ("/api/v1/admin/lr-exercise-bank/parts/{part}/imports", "post", "file"),
            ("/api/v1/admin/speaking/media/images", "post", "file"),
            ("/api/v1/admin/writing/exercises/imports/validate", "post", "file"),
            ("/api/v1/admin/writing/exercises/import", "post", "file"),
            ("/api/v1/admin/writing/media/images", "post", "file"),
            ("/api/v1/admin/writing/tests/imports/validate", "post", "file"),
            ("/api/v1/admin/writing/tests/import", "post", "file"),
            ("/api/v1/profile/avatar", "put", "file"),
            ("/api/v1/users/me/avatar", "put", "file"),
            ("/api/v1/pronunciation/assessments", "post", "file"),
            ("/api/v1/speaking/attempts/{attemptId}/recordings/{position}", "put", "file")
        ];
        foreach (var (path, method, fileField) in uploadOperations)
        {
            var operation = paths.GetProperty(path).GetProperty(method);
            var content = operation.GetProperty("requestBody").GetProperty("content");
            Assert.False(content.TryGetProperty("application/x-www-form-urlencoded", out _),
                $"{method.ToUpperInvariant()} {path} should not emit application/x-www-form-urlencoded.");
            var schema = content.GetProperty("multipart/form-data").GetProperty("schema");
            var field = schema.GetProperty("properties").GetProperty(fileField);
            Assert.Equal("string", field.GetProperty("type").GetString());
            Assert.Equal("binary", field.GetProperty("format").GetString());
        }

        // Rate-limited endpoints declare 429 ProblemDetails responses.
        (string Path, string Method)[] rateLimitedOperations =
        [
            ("/api/v1/profile/password", "put"),
            ("/api/v1/profile/password", "post"),
            ("/api/v1/users/me/password", "put"),
            ("/api/v1/users/me/password", "post"),
            ("/api/v1/speaking/attempts/{attemptId}/evaluation-consent", "post"),
            ("/api/v1/speaking/attempts/{attemptId}/recordings/{position}", "put"),
            ("/api/v1/pronunciation/assessments", "post")
        ];
        foreach (var (path, method) in rateLimitedOperations)
        {
            var responses = paths.GetProperty(path).GetProperty(method).GetProperty("responses");
            Assert.True(responses.TryGetProperty("429", out _),
                $"{method.ToUpperInvariant()} {path} must declare a 429 response in OpenAPI.");
        }
    }
}

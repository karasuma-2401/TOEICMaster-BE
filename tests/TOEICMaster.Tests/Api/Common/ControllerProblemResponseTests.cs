using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Api.Contracts;
using TOEICMaster.Application.Knowledge;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Tests.Api.Accounts;

namespace TOEICMaster.Tests.Api.Common;

public sealed class ControllerProblemResponseTests(AdminUsersApiFactory factory) : IClassFixture<AdminUsersApiFactory>
{
    [Theory]
    [InlineData("POST", "/api/v1/admin/writing/exercises/imports/validate?part=3")]
    [InlineData("POST", "/api/v1/admin/writing/exercises/import?part=3")]
    [InlineData("POST", "/api/v1/admin/writing/media/images")]
    [InlineData("POST", "/api/v1/admin/writing/tests/imports/validate")]
    [InlineData("POST", "/api/v1/admin/writing/tests/import")]
    [InlineData("POST", "/api/v1/admin/lr-exercise-bank/parts/3/imports")]
    [InlineData("PUT", "/api/v1/profile/avatar")]
    public async Task Empty_uploads_return_validation_problems_with_type_and_traceId(string method, string path)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([]), "file", "empty.xlsx");
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = content };

        using var response = await client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest,
            "https://tools.ietf.org/html/rfc9110#section-15.5.1");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotEmpty(document.RootElement.GetProperty("errors").GetProperty("file").EnumerateArray());
    }

    [Fact]
    public async Task Oversized_knowledge_images_return_a_typed_problem_instead_of_plain_json()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[KnowledgeImageSignature.MaxBytes + 1]), "file", "image.png");

        using var response = await client.PostAsync("/api/v1/admin/knowledge/media/images", content);

        await AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge,
            "https://tools.ietf.org/html/rfc9110#section-15.5.14");
    }

    [Fact]
    public async Task Pronunciation_errors_keep_the_error_code_and_standard_problem_fields()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([]), "file", "empty.wav");

        using var response = await client.PostAsync("/api/v1/pronunciation/assessments", content);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest,
            "https://tools.ietf.org/html/rfc9110#section-15.5.1");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("INVALID_AUDIO", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Writing_import_returns_a_typed_count_and_persists_each_imported_exercise()
    {
        var email = $"writing-import-{Guid.NewGuid():N}@example.test";
        var now = TimeProvider.System.GetUtcNow();
        var actor = User.CreateByAdmin("Import Admin", email, email.ToUpperInvariant(), "test-password-hash",
            UserRole.Admin, true, now, now);
        string[] codes = [$"IMPORT-{Guid.NewGuid():N}", $"IMPORT-{Guid.NewGuid():N}"];
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            db.Users.Add(actor);
            await db.SaveChangesAsync();
        }

        try
        {
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
            client.DefaultRequestHeaders.Add("X-Test-User-Id", actor.Id.ToString("D"));
            using var content = new MultipartFormDataContent();
            var workbook = new ByteArrayContent(CreateWorkbook(codes));
            workbook.Headers.ContentType = new MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            content.Add(workbook, "file", "exercises.xlsx");

            using var response = await client.PostAsync("/api/v1/admin/writing/exercises/import?part=3", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ImportWritingItemsResponse>();
            Assert.Equal(2, result!.ImportedCount);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(2, document.RootElement.GetProperty("importedCount").GetInt32());
            Assert.Single(document.RootElement.EnumerateObject());

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            var stored = await db.WritingExercises.AsNoTracking()
                .Where(exercise => codes.Contains(exercise.Code)).ToListAsync();
            Assert.Equal(codes.Order(), stored.Select(exercise => exercise.Code).Order());
            Assert.All(stored, exercise => Assert.Equal(actor.Id, exercise.CreatedByUserId));
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            await db.WritingExercises.Where(exercise => exercise.CreatedByUserId == actor.Id).ExecuteDeleteAsync();
            await db.Users.Where(user => user.Id == actor.Id).ExecuteDeleteAsync();
        }
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement;
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(type, problem.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    private static byte[] CreateWorkbook(IReadOnlyList<string> codes)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("xl/worksheets/sheet1.xml").Open(), Encoding.UTF8);
            writer.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            WriteRow(writer, 1, ["Code", "Topic", "Difficulty", "Status", "Prompt", "MinimumWordCount"]);
            for (var index = 0; index < codes.Count; index++)
                WriteRow(writer, index + 2, [codes[index], "Work", "MEDIUM", "DRAFT", "Explain your opinion.", "300"]);
            writer.Write("</sheetData></worksheet>");
        }
        return output.ToArray();
    }

    private static void WriteRow(TextWriter writer, int rowNumber, IReadOnlyList<string> values)
    {
        writer.Write($"<row r=\"{rowNumber}\">");
        for (var index = 0; index < values.Count; index++)
            writer.Write($"<c r=\"{(char)('A' + index)}{rowNumber}\" t=\"inlineStr\"><is><t>{values[index]}</t></is></c>");
        writer.Write("</row>");
    }
}

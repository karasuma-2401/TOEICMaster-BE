using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.Knowledge.Admin;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.Api.Common;
using TOEICMaster.Tests.Api.Exams;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Api.Knowledge;

public sealed class AdminKnowledgeApiTests : IClassFixture<AdminKnowledgeApiTests.KnowledgeApiFactory>
{
    private const string BasePath = "/api/v1/admin/knowledge";
    private readonly KnowledgeApiFactory _factory;

    public AdminKnowledgeApiTests(KnowledgeApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("articles")]
    [InlineData("categories")]
    [InlineData("media")]
    [InlineData("tags")]
    public async Task List_rejects_a_page_with_an_unrepresentable_offset(string resource)
    {
        using var client = CreateAdminClient();
        using var response = await client.GetAsync($"{BasePath}/{resource}?pageNumber={int.MaxValue}&pageSize=100");

        await AssertValidationErrorAsync(response, "Filter.PageNumber");
    }

    [Fact]
    public async Task Article_list_validates_the_documented_q_parameter()
    {
        using var client = CreateAdminClient();
        using var response = await client.GetAsync($"{BasePath}/articles?q={new string('a', 201)}");

        await AssertValidationErrorAsync(response, "Filter.Query");
    }

    [Fact]
    public async Task Create_article_rejects_an_empty_cover_asset_id()
    {
        using var client = CreateAdminClient();
        using var response = await client.PostAsJsonAsync($"{BasePath}/articles",
            ArticleInput("Empty cover test", """{"type":"doc"}""", Guid.Empty));

        await AssertValidationErrorAsync(response, "Input.CoverImageAssetId");
    }

    [Fact]
    public async Task Update_article_rejects_an_empty_cover_asset_id()
    {
        using var client = CreateAdminClient();
        using var response = await client.PutAsJsonAsync($"{BasePath}/articles/{Guid.NewGuid()}", new
        {
            version = 1,
            article = ArticleInput("Empty cover update test", """{"type":"doc"}""", Guid.Empty)
        });

        await AssertValidationErrorAsync(response, "Input.CoverImageAssetId");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upload_image_returns_field_errors_for_missing_or_invalid_file(bool includeInvalidFile)
    {
        using var client = CreateAdminClient();
        using var form = new MultipartFormDataContent();
        if (includeInvalidFile)
            form.Add(new ByteArrayContent([1, 2, 3]), "file", "test.png");
        else
            form.Add(new StringContent("test"), "description");

        using var response = await client.PostAsync($"{BasePath}/media/images", form);

        await AssertValidationErrorAsync(response, "file");
    }

    [Theory]
    [InlineData("""{"type":"doc","content":[{"type":"text","text":"Example","marks":[{"type":12}]}]}""", "application/json")]
    [InlineData("""{"type":"doc","content":[{"type":"heading","content":[{"type":false}]}]}""", "application/json")]
    [InlineData("""{"type":"doc","content":[{"type":"heading","content":[{"type":false}]}]}""", "text/html")]
    public async Task Create_article_returns_field_errors_for_invalid_content(string content, string accept)
    {
        using var client = CreateAdminClient();
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        using var response = await client.PostAsJsonAsync($"{BasePath}/articles",
            ArticleInput("Invalid content test", content));

        await AssertValidationErrorAsync(response, "content");
    }

    private object ArticleInput(string title, string content, Guid? coverImageAssetId = null)
    {
        using var document = JsonDocument.Parse(content);
        return new
        {
            title,
            summary = "Knowledge test article",
            skill = "General",
            categoryId = _factory.Category.Id,
            coverImageAssetId,
            content = document.RootElement.Clone()
        };
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        return client;
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(problem.GetProperty("errors").GetProperty(field).EnumerateArray());
    }

    public sealed class KnowledgeApiFactory : ApiFactory
    {
        public KnowledgeCategory Category { get; } = KnowledgeCategory.Create(
            "Knowledge test category", "knowledge-test-category", null, 0, Guid.NewGuid(), DateTimeOffset.UtcNow);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var repository = new InMemoryKnowledgeAdminRepository();
                repository.Categories.Add(Category);
                services.AddSingleton<IKnowledgeAdminRepository>(repository);
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, ExamTestAuthenticationHandler>("Test", _ => { });
            });
        }
    }
}

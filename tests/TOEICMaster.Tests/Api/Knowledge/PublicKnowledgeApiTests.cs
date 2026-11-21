using System.Net;
using System.Net.Http.Json;
using TOEICMaster.Application.Knowledge.Admin;

namespace TOEICMaster.Tests.Api.Knowledge;

public sealed class PublicKnowledgeApiTests : IClassFixture<AdminKnowledgeApiTests.KnowledgeApiFactory>
{
    private readonly AdminKnowledgeApiTests.KnowledgeApiFactory factory;
    public PublicKnowledgeApiTests(AdminKnowledgeApiTests.KnowledgeApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Guest_can_browse_but_cannot_read_saved_lessons()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/knowledge/articles?status=Draft");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<KnowledgeArticlePageDto>();
        Assert.All(page!.Items, a => Assert.Equal("Published", a.Status));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/knowledge/saved")).StatusCode);
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageSize=101")]
    [InlineData("skill=invalid")]
    [InlineData("sort=invalid")]
    public async Task Public_list_validates_filters(string query)
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/knowledge/articles?" + query)).StatusCode);
    }
}

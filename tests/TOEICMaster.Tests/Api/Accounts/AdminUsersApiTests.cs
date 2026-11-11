using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;

namespace TOEICMaster.Tests.Api.Accounts;

public sealed class AdminUsersApiTests(AccountsApiFactory factory) : IClassFixture<AccountsApiFactory>
{
    private const string Path = "/api/v1/admin/users";

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("User", HttpStatusCode.Forbidden)]
    public async Task User_list_requires_an_authenticated_admin(string? role, HttpStatusCode expected)
    {
        using var client = CreateClient(new RecordingReader(), role);
        using var response = await client.GetAsync(Path);
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task User_list_returns_the_shape_the_admin_page_parses()
    {
        using var client = CreateClient(new RecordingReader(), "Admin");
        using var response = await client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "items", "page", "pageSize", "stats", "totalItems", "totalPages" },
            page.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal(new[] { "activeUsers", "lockedUsers", "pendingUsers", "totalUsers" },
            page.GetProperty("stats").EnumerateObject().Select(property => property.Name).Order().ToArray());
        var user = page.GetProperty("items")[0];
        Assert.Equal(new[]
            {
                "authProvider", "avatarUrl", "createdAt", "email", "emailVerified", "fullName", "id", "lastLoginAt",
                "role", "status"
            },
            user.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal("ADMIN", user.GetProperty("role").GetString());
        Assert.Equal("ACTIVE", user.GetProperty("status").GetString());
        Assert.Equal("PASSWORD", user.GetProperty("authProvider").GetString());
    }

    [Fact]
    public async Task User_list_forwards_the_filters_from_the_query_string()
    {
        var reader = new RecordingReader();
        using var client = CreateClient(reader, "Admin");
        using var response = await client.GetAsync(
            Path + "?page=2&pageSize=20&q=an&role=USER&status=LOCKED&sort=lastLoginAt%3Aasc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new GetAdminUsersQuery(2, 20, "an", "USER", "LOCKED", "lastLoginAt:asc"), reader.LastQuery);
    }

    [Theory]
    [InlineData("?page=0", "Page")]
    [InlineData("?page=10001", "Page")]
    [InlineData("?pageSize=0", "PageSize")]
    [InlineData("?pageSize=101", "PageSize")]
    [InlineData("?role=OWNER", "Role")]
    [InlineData("?status=DELETED", "Status")]
    [InlineData("?sort=email:asc", "Sort")]
    public async Task Invalid_filters_return_400_with_field_errors(string query, string field)
    {
        using var client = CreateClient(new RecordingReader(), "Admin");
        using var response = await client.GetAsync(Path + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    private HttpClient CreateClient(IAdminUserReader reader, string? role)
    {
        var client = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton(reader))).CreateClient();
        if (role is not null)
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    private sealed class RecordingReader : IAdminUserReader
    {
        public GetAdminUsersQuery? LastQuery { get; private set; }

        public Task<AdminUserPageDto> GetPageAsync(GetAdminUsersQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            var user = new AdminUserDto(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Admin", "admin@example.com",
                null, "ADMIN", "ACTIVE", true, "PASSWORD", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), null);
            return Task.FromResult(new AdminUserPageDto([user], query.Page, query.PageSize, 1, 1,
                new AdminUserStatsDto(1, 1, 0, 0)));
        }
    }
}

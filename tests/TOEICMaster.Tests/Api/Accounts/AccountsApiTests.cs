using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Accounts.Queries.GetAccounts;
using TOEICMaster.Tests.Api.Common;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Api.Accounts;

public sealed class AccountsApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IAccountReader, SampleAccountReader>();
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultForbidScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, AccountTestAuthenticationHandler>("Test", _ => { });
        });
    }
}

// Only registered in the test host; production never trusts a role header.
public sealed class AccountTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "test-user"), new Claim(ClaimTypes.Role, role.ToString())], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

public sealed class AccountsApiTests(AccountsApiFactory factory) : IClassFixture<AccountsApiFactory>
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("User", HttpStatusCode.Forbidden)]
    public async Task Account_list_requires_an_authenticated_admin(string? role, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        if (role is not null)
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        using var response = await client.GetAsync("/api/v1/accounts");
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("", 1, 20, 20, 2, true, false)]
    [InlineData("?pageNumber=2&pageSize=10", 2, 10, 10, 3, true, true)]
    [InlineData("?pageNumber=3&pageSize=10", 3, 10, 3, 3, false, true)]
    [InlineData("?pageNumber=4&pageSize=10", 4, 10, 0, 3, false, true)]
    [InlineData("?pageNumber=1&pageSize=100", 1, 100, 23, 1, false, false)]
    [InlineData("?pageNumber=2147483647&pageSize=1", int.MaxValue, 1, 0, 23, false, true)]
    public async Task Account_list_returns_page_items_and_metadata(string query, int pageNumber,
        int pageSize, int count, int totalPages, bool hasNext, bool hasPrevious)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        using var response = await client.GetAsync("/api/v1/accounts" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(count, page.GetProperty("items").GetArrayLength());
        Assert.Equal(pageNumber, page.GetProperty("pageNumber").GetInt32());
        Assert.Equal(pageSize, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(23, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(totalPages, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(hasNext, page.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(hasPrevious, page.GetProperty("hasPreviousPage").GetBoolean());
        if (count > 0)
        {
            var account = page.GetProperty("items")[0];
            Assert.Equal($"user{(pageNumber - 1) * pageSize + 1}@example.com", account.GetProperty("email").GetString());
            Assert.Equal($"User {(pageNumber - 1) * pageSize + 1}", account.GetProperty("displayName").GetString());
            Assert.Equal(new[] { "createdAtUtc", "displayName", "email", "id", "isActive" },
                account.EnumerateObject().Select(property => property.Name).Order().ToArray());
        }
    }

    [Theory]
    [InlineData("?pageNumber=0", "PageNumber")]
    [InlineData("?pageNumber=-1", "PageNumber")]
    [InlineData("?pageSize=0", "PageSize")]
    [InlineData("?pageSize=-1", "PageSize")]
    [InlineData("?pageSize=101", "PageSize")]
    [InlineData("?pageNumber=2147483647&pageSize=100", "PageNumber")]
    [InlineData("?pageNumber=abc", "pageNumber")]
    public async Task Invalid_pagination_returns_400_with_field_errors(string query, string field)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        using var response = await client.GetAsync("/api/v1/accounts" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public void Empty_page_has_zero_total_pages_and_no_next_page()
    {
        var page = new AccountPageDto([], 1, 20, 0);
        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasNextPage);
        Assert.False(page.HasPreviousPage);
    }

    [Fact]
    public async Task Empty_database_returns_an_empty_list_with_zero_total_count()
    {
        using var emptyFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IAccountReader>(new EmptyAccountReader())));
        using var client = emptyFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        var page = await client.GetFromJsonAsync<AccountPageDto>("/api/v1/accounts");
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasNextPage);
        Assert.False(page.HasPreviousPage);
    }

    [Fact]
    public async Task Query_handler_propagates_request_cancellation()
    {
        var handler = new GetAccountsQueryHandler(new SampleAccountReader());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.Handle(new GetAccountsQuery(), cancellation.Token));
    }

    private sealed class EmptyAccountReader : IAccountReader
    {
        public Task<AccountPageDto> GetPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult(new AccountPageDto([], pageNumber, pageSize, 0));
    }
}

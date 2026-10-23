using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Tests.Api.Accounts;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Api.Writing;

public sealed class WritingAdministrationAuthorizationTests(AccountsApiFactory factory)
    : IClassFixture<AccountsApiFactory>
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("User", HttpStatusCode.Forbidden)]
    public async Task Writing_administration_requires_an_authenticated_admin(string? role, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        if (role is not null)
            client.DefaultRequestHeaders.Add("X-Test-Role", role);

        using var response = await client.GetAsync("/api/v1/admin/writing/exercises");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_read_an_empty_writing_bank()
    {
        using var configuredFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IWritingAdminRepository, EmptyWritingAdminRepository>()));
        using var client = configuredFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        using var response = await client.GetAsync("/api/v1/admin/writing/exercises");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

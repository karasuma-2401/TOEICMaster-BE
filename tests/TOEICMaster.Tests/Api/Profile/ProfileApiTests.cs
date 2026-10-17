using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.Common.Security;
using TOEICMaster.Application.Profile;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.Api.Common;

namespace TOEICMaster.Tests.Api.Profile;

public sealed class ProfileApiTests
{
    [Fact]
    public async Task Profile_endpoints_require_a_bearer_token()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PatchAsJsonAsync("/api/v1/profile",
            new { displayName = "New name" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/v1/profile/password",
            new { currentPassword = "old-password", newPassword = "new-password" })).StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_can_read_only_their_own_profile()
    {
        var user = User.CreateExternal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", DateTimeOffset.UtcNow);
        var repository = new FakeProfileRepository(user);
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IProfileRepository>(repository)));
        using var client = AuthenticatedClient(factory, user);

        using var response = await client.GetAsync("/api/v1/profile");
        var profile = await response.Content.ReadFromJsonAsync<ProfileDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(user.Id, repository.LastRequestedUserId);
        Assert.Equal(user.Id, profile?.Id);
        Assert.Equal(user.Email, profile?.Email);
    }

    [Fact]
    public async Task Avatar_upload_rejects_non_image_content_before_storage()
    {
        var user = User.CreateExternal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", DateTimeOffset.UtcNow);
        var repository = new FakeProfileRepository(user);
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IProfileRepository>(repository);
            services.AddSingleton<IAvatarStore>(new FakeAvatarStore());
        }));
        using var client = AuthenticatedClient(factory, user);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("not an image"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "avatar.png");

        using var response = await client.PutAsync("/api/v1/profile/avatar", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(repository.AvatarKey);
    }

    [Fact]
    public async Task Authenticated_user_can_upload_a_png_avatar()
    {
        var user = User.CreateExternal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", DateTimeOffset.UtcNow);
        var repository = new FakeProfileRepository(user);
        var store = new FakeAvatarStore();
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IProfileRepository>(repository);
            services.AddSingleton<IAvatarStore>(store);
        }));
        using var client = AuthenticatedClient(factory, user);
        using var form = new MultipartFormDataContent();
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lWQAAAAASUVORK5CYII=");
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "avatar.png");

        using var response = await client.PutAsync("/api/v1/profile/avatar", form);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.StartsWith($"avatars/{user.Id:N}/", repository.AvatarKey);
        Assert.Equal(repository.AvatarKey, store.UploadedKey);
    }

    private static HttpClient AuthenticatedClient(WebApplicationFactory<Program> factory, User user)
    {
        var token = factory.Services.GetRequiredService<IAuthenticationTokenService>()
            .IssueForLogin(user, DateTimeOffset.UtcNow, false).AccessToken;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class FakeProfileRepository(User user) : IProfileRepository
    {
        public Guid LastRequestedUserId { get; private set; }
        public string? AvatarKey { get; private set; }

        public Task<User?> FindAsync(Guid userId, CancellationToken cancellationToken)
        {
            LastRequestedUserId = userId;
            return Task.FromResult(userId == user.Id ? user : null);
        }

        public Task<bool> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken) =>
            Task.FromResult(userId == user.Id);

        public Task<bool> UpdateAvatarAsync(Guid userId, string objectKey, CancellationToken cancellationToken)
        {
            AvatarKey = objectKey;
            return Task.FromResult(userId == user.Id);
        }

        public Task<bool> ChangePasswordAsync(Guid userId, string currentHash, string newHash,
            DateTimeOffset changedAtUtc, CancellationToken cancellationToken) => Task.FromResult(userId == user.Id);
    }

    private sealed class FakeAvatarStore : IAvatarStore
    {
        public string? UploadedKey { get; private set; }
        public Task UploadAsync(string objectKey, byte[] content, string contentType,
            CancellationToken cancellationToken)
        {
            UploadedKey = objectKey;
            return Task.CompletedTask;
        }
        public Task<AvatarContent> DownloadAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(new AvatarContent([], "image/png"));
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

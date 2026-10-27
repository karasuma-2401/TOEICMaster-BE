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
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/v1/profile",
            new { displayName = "New name", phone = (string?)null, targetScore = (int?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/v1/profile/password",
            new { currentPassword = "old-password", newPassword = "new-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/v1/users/me",
            new
            {
                displayName = "New name",
                phone = (string?)null,
                dateOfBirth = (string?)null,
                targetScore = (int?)null
            })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/users/me/password",
            new
            {
                currentPassword = "old-password",
                newPassword = "new-password",
                confirmPassword = "new-password"
            })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/v1/users/me/avatar")).StatusCode);
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
        Assert.False(profile?.HasPassword);
    }

    [Fact]
    public async Task Put_updates_phone_and_target_score_and_rejects_invalid_values()
    {
        var user = User.CreateExternal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", DateTimeOffset.UtcNow);
        var repository = new FakeProfileRepository(user);
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IProfileRepository>(repository)));
        using var client = AuthenticatedClient(factory, user);

        using var updated = await client.PutAsJsonAsync("/api/v1/profile",
            new { displayName = "Linh", phone = "0912345678", targetScore = 750 });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(("Linh", "0912345678", 750), repository.UpdatedDetails);

        using var invalid = await client.PutAsJsonAsync("/api/v1/profile",
            new { displayName = "Linh", phone = "123", targetScore = 752 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Frontend_profile_contract_can_read_and_update_all_editable_fields()
    {
        var user = User.CreateExternal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", DateTimeOffset.UtcNow);
        var repository = new FakeProfileRepository(user);
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IProfileRepository>(repository)));
        using var client = AuthenticatedClient(factory, user);

        using var getResponse = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var updateResponse = await client.PutAsJsonAsync("/api/v1/users/me", new
        {
            displayName = "  New name  ",
            phone = "+84912345678",
            dateOfBirth = "2001-06-15",
            targetScore = 850
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var profile = await updateResponse.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.Equal("New name", profile?.DisplayName);
        Assert.Equal("+84912345678", profile?.Phone);
        Assert.Equal(new DateOnly(2001, 6, 15), profile?.DateOfBirth);
        Assert.Equal(850, profile?.TargetScore);
        Assert.True(profile?.HasPassword is false);
    }

    [Fact]
    public async Task Patch_name_does_not_overwrite_personal_details_changed_concurrently()
    {
        var user = User.CreateExternal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", DateTimeOffset.UtcNow);
        user.UpdateProfile("Learner", "0912345678", new DateOnly(2001, 6, 15), 750);
        var repository = new FakeProfileRepository(user)
        {
            BeforeNameUpdate = () => user.UpdateProfile("Learner", "0987654321", new DateOnly(2002, 7, 16), 850)
        };
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IProfileRepository>(repository)));
        using var client = AuthenticatedClient(factory, user);

        using var response = await client.PatchAsJsonAsync("/api/v1/profile", new { displayName = "New name" });
        var profile = await response.Content.ReadFromJsonAsync<ProfileDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("New name", profile?.DisplayName);
        Assert.Equal("0987654321", profile?.Phone);
        Assert.Equal(new DateOnly(2002, 7, 16), profile?.DateOfBirth);
        Assert.Equal(850, profile?.TargetScore);
    }

    [Fact]
    public async Task Frontend_profile_contract_rejects_invalid_personal_details()
    {
        var user = User.CreateExternal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM", DateTimeOffset.UtcNow);
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IProfileRepository>(new FakeProfileRepository(user))));
        using var client = AuthenticatedClient(factory, user);

        using var response = await client.PutAsJsonAsync("/api/v1/users/me", new
        {
            displayName = "Learner",
            phone = "not-a-phone",
            dateOfBirth = "2999-01-01",
            targetScore = 851
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith($"avatars/{user.Id:N}/", repository.AvatarKey);
        Assert.Equal(repository.AvatarKey, store.UploadedKey);
    }

    [Fact]
    public async Task Frontend_password_contract_requires_confirmation_and_changes_the_password()
    {
        using var baseFactory = new ApiFactory();
        var hasher = baseFactory.Services.GetRequiredService<IPasswordHasher>();
        var user = User.CreateLocal("Learner", "learner@example.com", "LEARNER@EXAMPLE.COM",
            hasher.Hash("old-password"), DateTimeOffset.UtcNow);
        user.VerifyEmail(DateTimeOffset.UtcNow);
        var repository = new FakeProfileRepository(user);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IProfileRepository>(repository)));
        using var client = AuthenticatedClient(factory, user);

        using var mismatch = await client.PostAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = "old-password",
            newPassword = "new-password",
            confirmPassword = "different-password"
        });
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);

        using var response = await client.PostAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = "old-password",
            newPassword = "new-password",
            confirmPassword = "new-password"
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Frontend_avatar_upload_returns_the_updated_profile_and_delete_removes_the_object()
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

        using var uploadResponse = await client.PutAsync("/api/v1/users/me/avatar", form);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        var profile = await uploadResponse.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.False(string.IsNullOrWhiteSpace(profile?.AvatarVersion));

        using var deleteResponse = await client.DeleteAsync("/api/v1/users/me/avatar");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(store.UploadedKey, store.DeletedKey);
        Assert.Null(repository.AvatarKey);
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
        public Action? BeforeNameUpdate { get; init; }
        public (string DisplayName, string? Phone, int? TargetScore)? UpdatedDetails { get; private set; }

        public Task<User?> FindAsync(Guid userId, CancellationToken cancellationToken)
        {
            LastRequestedUserId = userId;
            return Task.FromResult(userId == user.Id ? user : null);
        }

        public Task<bool> UpdateDisplayNameAsync(Guid userId, string displayName,
            CancellationToken cancellationToken)
        {
            if (userId != user.Id)
                return Task.FromResult(false);
            BeforeNameUpdate?.Invoke();
            user.UpdateProfile(displayName, user.Phone, user.DateOfBirth, user.TargetScore);
            return Task.FromResult(true);
        }

        public Task<bool> UpdateProfileAsync(Guid userId, string displayName, string? phone, DateOnly? dateOfBirth,
            int? targetScore, CancellationToken cancellationToken)
        {
            if (userId != user.Id)
                return Task.FromResult(false);
            UpdatedDetails = (displayName, phone, targetScore);
            user.UpdateProfile(displayName, phone, dateOfBirth, targetScore);
            return Task.FromResult(true);
        }

        public Task<bool> UpdateDetailsAsync(Guid userId, string displayName, string? phone, int? targetScore,
            CancellationToken cancellationToken)
        {
            UpdatedDetails = (displayName, phone, targetScore);
            return Task.FromResult(userId == user.Id);
        }

        public Task<bool> UpdateAvatarAsync(Guid userId, string objectKey, CancellationToken cancellationToken)
        {
            AvatarKey = objectKey;
            user.SetAvatarObjectKey(objectKey);
            return Task.FromResult(userId == user.Id);
        }

        public Task<string?> ClearAvatarAsync(Guid userId, CancellationToken cancellationToken)
        {
            if (userId != user.Id)
                return Task.FromResult<string?>(null);
            var objectKey = AvatarKey;
            AvatarKey = null;
            user.SetAvatarObjectKey(null);
            return Task.FromResult(objectKey);
        }

        public Task<bool> ChangePasswordAsync(Guid userId, string currentHash, string newHash,
            DateTimeOffset changedAtUtc, CancellationToken cancellationToken) => Task.FromResult(userId == user.Id);
    }

    private sealed class FakeAvatarStore : IAvatarStore
    {
        public string? UploadedKey { get; private set; }
        public string? DeletedKey { get; private set; }
        public Task UploadAsync(string objectKey, byte[] content, string contentType,
            CancellationToken cancellationToken)
        {
            UploadedKey = objectKey;
            return Task.CompletedTask;
        }
        public Task<AvatarContent> DownloadAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(new AvatarContent([], "image/png"));
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
        {
            DeletedKey = objectKey;
            return Task.CompletedTask;
        }
    }
}

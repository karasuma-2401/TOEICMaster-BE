using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Accounts.Queries.GetAdminUsers;
using TOEICMaster.Application.Email;
using TOEICMaster.Application.Profile;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Tests.Api.Common;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Api.Accounts;

public class AdminUsersApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultForbidScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, AdminTestAuthenticationHandler>("Test", _ => { });
        });
    }
}

// Only registered in the test host; production never trusts test role or user headers.
public sealed class AdminTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = Request.Headers.TryGetValue("X-Test-User-Id", out var rawUserId) &&
                     Guid.TryParse(rawUserId, out var parsed)
            ? parsed.ToString("D")
            : "00000000-0000-0000-0000-000000000001";

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role.ToString())],
            Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

public sealed class AdminUsersApiTests(AdminUsersApiFactory factory) : IClassFixture<AdminUsersApiFactory>
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

    [Theory]
    [InlineData("GET", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002", null, HttpStatusCode.Unauthorized)]
    [InlineData("GET", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002", "User", HttpStatusCode.Forbidden)]
    [InlineData("POST", "/api/v1/admin/users", null, HttpStatusCode.Unauthorized)]
    [InlineData("POST", "/api/v1/admin/users", "User", HttpStatusCode.Forbidden)]
    [InlineData("PATCH", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002", null, HttpStatusCode.Unauthorized)]
    [InlineData("PATCH", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002", "User", HttpStatusCode.Forbidden)]
    [InlineData("DELETE", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002", null, HttpStatusCode.Unauthorized)]
    [InlineData("DELETE", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002", "User", HttpStatusCode.Forbidden)]
    [InlineData("POST", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002/lock", null, HttpStatusCode.Unauthorized)]
    [InlineData("POST", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002/lock", "User", HttpStatusCode.Forbidden)]
    [InlineData("DELETE", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002/lock", null, HttpStatusCode.Unauthorized)]
    [InlineData("DELETE", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002/lock", "User", HttpStatusCode.Forbidden)]
    [InlineData("POST", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002/reset-password", null, HttpStatusCode.Unauthorized)]
    [InlineData("POST", "/api/v1/admin/users/00000000-0000-0000-0000-000000000002/reset-password", "User", HttpStatusCode.Forbidden)]
    public async Task All_admin_user_endpoints_require_authenticated_admin(string method, string url, string? role,
        HttpStatusCode expected)
    {
        using var client = CreateClient(new RecordingReader(), role);
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST" && url == Path)
            request.Content = JsonContent.Create(new { fullName = "A", email = "a@example.com", role = "USER", sendActivationEmail = false });
        else if (method == "PATCH")
            request.Content = JsonContent.Create(new { role = "USER" });

        using var response = await client.SendAsync(request);
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

    [Fact]
    public async Task End_to_end_admin_user_lifecycle_works_against_postgres()
    {
        await using var dbHost = await IsolatedPostgresHost.CreateAsync();
        using var client = dbHost.CreateAdminClient(dbHost.PrimaryAdminId);

        // 1. GET /api/v1/admin/users/{id} (200 & 404)
        using var getMissing = await client.GetAsync($"{Path}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, getMissing.StatusCode);
        var missingProblem = await getMissing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(missingProblem.GetProperty("traceId").GetString()));

        // Entrypoint Parity: 404 NotFound ProblemDetails with traceId for all single-user endpoints
        var unknownId = Guid.NewGuid();
        foreach (var (method, suffix, body) in new (HttpMethod, string, object?)[]
                 {
                     (HttpMethod.Patch, $"{Path}/{unknownId}", new { role = "USER" }),
                     (HttpMethod.Delete, $"{Path}/{unknownId}", null),
                     (HttpMethod.Post, $"{Path}/{unknownId}/lock", null),
                     (HttpMethod.Delete, $"{Path}/{unknownId}/lock", null),
                     (HttpMethod.Post, $"{Path}/{unknownId}/reset-password", null)
                 })
        {
            using var req = new HttpRequestMessage(method, suffix);
            if (body is not null)
                req.Content = JsonContent.Create(body);
            using var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            var notFoundProblem = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrEmpty(notFoundProblem.GetProperty("traceId").GetString()));
        }

        using var getAdmin = await client.GetAsync($"{Path}/{dbHost.PrimaryAdminId}");
        Assert.Equal(HttpStatusCode.OK, getAdmin.StatusCode);
        var adminDto = await getAdmin.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[]
            {
                "authProvider", "avatarUrl", "createdAt", "email", "emailVerified", "fullName", "id", "lastLoginAt",
                "role", "status"
            },
            adminDto.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal("ADMIN", adminDto.GetProperty("role").GetString());

        // 2. POST /api/v1/admin/users (sendActivationEmail = true -> PENDING + mail sent)
        using var createPending = await client.PostAsJsonAsync(Path, new
        {
            fullName = "Pending Learner",
            email = "pending@example.com",
            role = "USER",
            sendActivationEmail = true
        });
        Assert.Equal(HttpStatusCode.Created, createPending.StatusCode);
        Assert.NotNull(createPending.Headers.Location);
        var pendingJson = await createPending.Content.ReadFromJsonAsync<JsonElement>();
        var pendingId = pendingJson.GetProperty("id").GetGuid();
        Assert.Equal("PENDING", pendingJson.GetProperty("status").GetString());
        Assert.False(pendingJson.GetProperty("emailVerified").GetBoolean());
        Assert.Equal("PASSWORD", pendingJson.GetProperty("authProvider").GetString());
        Assert.Single(dbHost.MailSender.Messages);

        // Pending user cannot be locked or unlocked or reset-password
        using var lockPending = await client.PostAsync($"{Path}/{pendingId}/lock", null);
        Assert.Equal(HttpStatusCode.Conflict, lockPending.StatusCode);
        Assert.Equal("USER_PENDING_VERIFICATION",
            (await lockPending.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var unlockPending = await client.DeleteAsync($"{Path}/{pendingId}/lock");
        Assert.Equal(HttpStatusCode.Conflict, unlockPending.StatusCode);
        Assert.Equal("USER_PENDING_VERIFICATION",
            (await unlockPending.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var resetPending = await client.PostAsync($"{Path}/{pendingId}/reset-password", null);
        Assert.Equal(HttpStatusCode.Conflict, resetPending.StatusCode);
        Assert.Equal("USER_INACTIVE_OR_UNVERIFIED",
            (await resetPending.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        // 3. POST /api/v1/admin/users (sendActivationEmail = false -> ACTIVE, no mail, duplicate email -> 409)
        using var createActive = await client.PostAsJsonAsync(Path, new
        {
            fullName = "Active Learner",
            email = "active@example.com",
            role = "USER",
            sendActivationEmail = false
        });
        Assert.Equal(HttpStatusCode.Created, createActive.StatusCode);
        var activeJson = await createActive.Content.ReadFromJsonAsync<JsonElement>();
        var activeId = activeJson.GetProperty("id").GetGuid();
        Assert.Equal("ACTIVE", activeJson.GetProperty("status").GetString());
        Assert.True(activeJson.GetProperty("emailVerified").GetBoolean());
        Assert.Single(dbHost.MailSender.Messages); // Still 1 email total

        using var createDuplicate = await client.PostAsJsonAsync(Path, new
        {
            fullName = "Dup Learner",
            email = "ACTIVE@example.com",
            role = "USER",
            sendActivationEmail = false
        });
        Assert.Equal(HttpStatusCode.Conflict, createDuplicate.StatusCode);

        // Single-Fault Isolation 400 checks on POST /api/v1/admin/users
        foreach (var (payload, expectedErrorKey) in new (object, string)[]
                 {
                     (new { fullName = "", email = "valid@example.com", role = "USER", sendActivationEmail = false }, "FullName"),
                     (new { fullName = "Valid Name", email = "bad-email", role = "USER", sendActivationEmail = false }, "Email"),
                     (new { fullName = "Valid Name", email = "valid@example.com", role = "OWNER", sendActivationEmail = false }, "Role")
                 })
        {
            using var createInvalid = await client.PostAsJsonAsync(Path, payload);
            Assert.Equal(HttpStatusCode.BadRequest, createInvalid.StatusCode);
            var validationProblem = await createInvalid.Content.ReadFromJsonAsync<JsonElement>();
            var errorKeys = validationProblem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray();
            Assert.Equal(new[] { expectedErrorKey }, errorKeys);
        }

        // Single-Fault Isolation 400 check on PATCH /api/v1/admin/users/{id}
        using var patchInvalidRole = await client.PatchAsJsonAsync($"{Path}/{activeId}", new { role = "OWNER" });
        Assert.Equal(HttpStatusCode.BadRequest, patchInvalidRole.StatusCode);
        var patchProblem = await patchInvalidRole.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "Role" },
            patchProblem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray());

        // Idempotent role update when role is unchanged
        using var patchSameRole = await client.PatchAsJsonAsync($"{Path}/{activeId}", new { role = "USER" });
        Assert.Equal(HttpStatusCode.OK, patchSameRole.StatusCode);
        Assert.Equal("USER", (await patchSameRole.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());

        // 4. PATCH /api/v1/admin/users/{id} (promote, idempotent, demote revokes sessions, self-demote & last-admin guards)
        using var selfDemote = await client.PatchAsJsonAsync($"{Path}/{dbHost.PrimaryAdminId}", new { role = "USER" });
        Assert.Equal(HttpStatusCode.Conflict, selfDemote.StatusCode);
        Assert.Equal("CANNOT_DEMOTE_SELF",
            (await selfDemote.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var selfIdempotent = await client.PatchAsJsonAsync($"{Path}/{dbHost.PrimaryAdminId}", new { role = "ADMIN" });
        Assert.Equal(HttpStatusCode.OK, selfIdempotent.StatusCode);

        // Promote activeId -> ADMIN, then have activeId fail to demote PrimaryAdmin? No, both are active admins, so demoting activeId back succeeds and revokes sessions
        await dbHost.SeedSessionChainAsync(activeId);
        using var promoteUser = await client.PatchAsJsonAsync($"{Path}/{activeId}", new { role = "ADMIN" });
        Assert.Equal(HttpStatusCode.OK, promoteUser.StatusCode);
        Assert.Equal("ADMIN", (await promoteUser.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());

        using var demoteUser = await client.PatchAsJsonAsync($"{Path}/{activeId}", new { role = "USER" });
        Assert.Equal(HttpStatusCode.OK, demoteUser.StatusCode);
        Assert.Equal("USER", (await demoteUser.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
        Assert.True(await dbHost.AllSessionsRevokedAsync(activeId));

        // Second admin trying to demote last active admin (only PrimaryAdmin is active admin, or PrimaryAdmin tries to lock/delete self vs another admin demoting PrimaryAdmin)
        await dbHost.SeedSecondaryAdminAsync();
        using var secondAdminClient = dbHost.CreateAdminClient(dbHost.SecondaryAdminId);
        using var demotePrimary = await secondAdminClient.PatchAsJsonAsync($"{Path}/{dbHost.PrimaryAdminId}", new { role = "USER" });
        Assert.Equal(HttpStatusCode.OK, demotePrimary.StatusCode);
        // Now SecondaryAdmin is the ONLY active admin; PrimaryAdmin (as actor) trying to demote/lock/delete SecondaryAdmin hits LAST_ACTIVE_ADMIN
        using var demoteLastAdmin = await client.PatchAsJsonAsync($"{Path}/{dbHost.SecondaryAdminId}", new { role = "USER" });
        Assert.Equal(HttpStatusCode.Conflict, demoteLastAdmin.StatusCode);
        Assert.Equal("LAST_ACTIVE_ADMIN",
            (await demoteLastAdmin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var lockLastAdmin = await client.PostAsync($"{Path}/{dbHost.SecondaryAdminId}/lock", null);
        Assert.Equal(HttpStatusCode.Conflict, lockLastAdmin.StatusCode);
        Assert.Equal("LAST_ACTIVE_ADMIN",
            (await lockLastAdmin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var deleteLastAdmin = await client.DeleteAsync($"{Path}/{dbHost.SecondaryAdminId}");
        Assert.Equal(HttpStatusCode.Conflict, deleteLastAdmin.StatusCode);
        Assert.Equal("LAST_ACTIVE_ADMIN",
            (await deleteLastAdmin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        // Restore PrimaryAdmin as ADMIN so remaining tests run cleanly
        await secondAdminClient.PatchAsJsonAsync($"{Path}/{dbHost.PrimaryAdminId}", new { role = "ADMIN" });

        // 5. Lock / Unlock + ResetPassword (eligible, cooldown 429, Google 409, locked 409)
        using var selfLock = await client.PostAsync($"{Path}/{dbHost.PrimaryAdminId}/lock", null);
        Assert.Equal(HttpStatusCode.Conflict, selfLock.StatusCode);
        Assert.Equal("CANNOT_LOCK_SELF",
            (await selfLock.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        await dbHost.SeedActiveSessionAndResetChallengeAsync(activeId);
        using var lockActive = await client.PostAsync($"{Path}/{activeId}/lock", null);
        Assert.Equal(HttpStatusCode.NoContent, lockActive.StatusCode);
        Assert.True(await dbHost.AllSessionsRevokedAsync(activeId));
        Assert.False(await dbHost.HasResetChallengeAsync(activeId));

        using var resetLocked = await client.PostAsync($"{Path}/{activeId}/reset-password", null);
        Assert.Equal(HttpStatusCode.Conflict, resetLocked.StatusCode);
        Assert.Equal("USER_INACTIVE_OR_UNVERIFIED",
            (await resetLocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var unlockActive = await client.DeleteAsync($"{Path}/{activeId}/lock");
        Assert.Equal(HttpStatusCode.NoContent, unlockActive.StatusCode);

        // Idempotent unlock when user is already ACTIVE
        using var unlockAlreadyActive = await client.DeleteAsync($"{Path}/{activeId}/lock");
        Assert.Equal(HttpStatusCode.NoContent, unlockAlreadyActive.StatusCode);

        using var resetActive = await client.PostAsync($"{Path}/{activeId}/reset-password", null);
        Assert.Equal(HttpStatusCode.NoContent, resetActive.StatusCode);
        Assert.True(await dbHost.HasResetChallengeAsync(activeId));
        Assert.Equal(2, dbHost.MailSender.Messages.Count);

        using var resetCooldown = await client.PostAsync($"{Path}/{activeId}/reset-password", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, resetCooldown.StatusCode);
        Assert.Equal("PASSWORD_RESET_COOLDOWN",
            (await resetCooldown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var googleUserId = await dbHost.SeedGoogleUserAsync();
        using var resetGoogle = await client.PostAsync($"{Path}/{googleUserId}/reset-password", null);
        Assert.Equal(HttpStatusCode.Conflict, resetGoogle.StatusCode);
        Assert.Equal("PASSWORD_RESET_UNSUPPORTED_PROVIDER",
            (await resetGoogle.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        // 6. DELETE /api/v1/admin/users/{id} (self-delete 409, session ReplacedBySessionId cleanup + avatar delete 204, Knowledge FK Restrict 409)
        using var selfDelete = await client.DeleteAsync($"{Path}/{dbHost.PrimaryAdminId}");
        Assert.Equal(HttpStatusCode.Conflict, selfDelete.StatusCode);
        Assert.Equal("CANNOT_DELETE_SELF",
            (await selfDelete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        await dbHost.SetAvatarAndSessionChainAsync(activeId, "avatars/active-test.png");
        using var deleteActive = await client.DeleteAsync($"{Path}/{activeId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteActive.StatusCode);
        Assert.Contains("avatars/active-test.png", dbHost.AvatarStore.DeletedKeys);

        using var getDeleted = await client.GetAsync($"{Path}/{activeId}");
        Assert.Equal(HttpStatusCode.NotFound, getDeleted.StatusCode);

        var knowledgeAuthorId = await dbHost.SeedUserWithKnowledgeCategoryAsync();
        using var deleteAuthor = await client.DeleteAsync($"{Path}/{knowledgeAuthorId}");
        Assert.Equal(HttpStatusCode.Conflict, deleteAuthor.StatusCode);
        Assert.Equal("USER_HAS_REFERENCED_CONTENT",
            (await deleteAuthor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
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

    private sealed class IsolatedPostgresHost : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly string _connectionString;

        public Guid PrimaryAdminId { get; }
        public Guid SecondaryAdminId { get; private set; }
        public RecordingMailSender MailSender { get; } = new();
        public RecordingAvatarStore AvatarStore { get; } = new();

        private IsolatedPostgresHost(string connectionString, Guid primaryAdminId)
        {
            _connectionString = connectionString;
            PrimaryAdminId = primaryAdminId;
            _factory = new AdminUsersApiFactory().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Startup:SkipSeeders"] = "true",
                        ["ConnectionStrings:Postgres"] = _connectionString
                    }));
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IMailSender>();
                    services.AddSingleton<IMailSender>(MailSender);
                    services.RemoveAll<IAvatarStore>();
                    services.AddSingleton<IAvatarStore>(AvatarStore);
                });
            });
        }

        public static async Task<IsolatedPostgresHost> CreateAsync()
        {
            var baseConnection = Environment.GetEnvironmentVariable("TOEICMASTER_TEST_POSTGRES")
                ?? throw new InvalidOperationException("TOEICMASTER_TEST_POSTGRES is required for Postgres tests.");
            var dbConnection = new NpgsqlConnectionStringBuilder(baseConnection)
            {
                Database = $"toeicmaster_adminusers_{Guid.NewGuid():N}",
                Pooling = false
            }.ConnectionString;

            var options = new DbContextOptionsBuilder<ToeicDbContext>()
                .UseNpgsql(dbConnection, provider => provider.EnableRetryOnFailure(3)).Options;

            var admin = User.CreateByAdmin("Primary Admin", "primary@example.com", "PRIMARY@EXAMPLE.COM",
                "hash", UserRole.Admin, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

            await using (var db = new ToeicDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Users.Add(admin);
                await db.SaveChangesAsync();
            }

            return new IsolatedPostgresHost(dbConnection, admin.Id);
        }

        public HttpClient CreateAdminClient(Guid actorId)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
            client.DefaultRequestHeaders.Add("X-Test-User-Id", actorId.ToString("D"));
            return client;
        }

        public async Task SeedSecondaryAdminAsync()
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            var secondary = User.CreateByAdmin("Secondary Admin", "secondary@example.com", "SECONDARY@EXAMPLE.COM",
                "hash", UserRole.Admin, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            db.Users.Add(secondary);
            await db.SaveChangesAsync();
            SecondaryAdminId = secondary.Id;
        }

        public async Task SeedSessionChainAsync(Guid userId)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            var now = DateTimeOffset.UtcNow;
            var s1 = UserSession.Create(userId, new string('a', 64), now, now.AddDays(1));
            var s2 = UserSession.Create(userId, new string('b', 64), now.AddMinutes(1), now.AddDays(1));
            db.UserSessions.AddRange(s1, s2);
            await db.SaveChangesAsync();
            await db.UserSessions.Where(s => s.Id == s1.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ReplacedBySessionId, s2.Id));
        }

        public async Task SeedActiveSessionAndResetChallengeAsync(Guid userId)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.UserSessions.Add(UserSession.Create(userId, new string('c', 64), now, now.AddDays(1)));
            if (!await db.PasswordResetChallenges.AnyAsync(c => c.UserId == userId))
                db.PasswordResetChallenges.Add(PasswordResetChallenge.Create(userId, new string('d', 64), now, now.AddMinutes(10)));
            await db.SaveChangesAsync();
        }

        public async Task SetAvatarAndSessionChainAsync(Guid userId, string objectKey)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            await db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.AvatarObjectKey, objectKey));
            var now = DateTimeOffset.UtcNow;
            var s1 = UserSession.Create(userId, new string('e', 64), now, now.AddDays(1));
            var s2 = UserSession.Create(userId, new string('f', 64), now.AddMinutes(1), now.AddDays(1));
            db.UserSessions.AddRange(s1, s2);
            await db.SaveChangesAsync();
            await db.UserSessions.Where(s => s.Id == s1.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ReplacedBySessionId, s2.Id));
        }

        public async Task<bool> AllSessionsRevokedAsync(Guid userId)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            return !await db.UserSessions.AnyAsync(s => s.UserId == userId && s.RevokedAtUtc == null);
        }

        public async Task<bool> HasResetChallengeAsync(Guid userId)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            return await db.PasswordResetChallenges.AnyAsync(c => c.UserId == userId);
        }

        public async Task<Guid> SeedGoogleUserAsync()
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            var user = User.CreateExternal("Google User", "google@example.com", "GOOGLE@EXAMPLE.COM",
                DateTimeOffset.UtcNow);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        }

        public async Task<Guid> SeedUserWithKnowledgeCategoryAsync()
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            var now = DateTimeOffset.UtcNow;
            var author = User.CreateByAdmin("Knowledge Author", "author@example.com", "AUTHOR@EXAMPLE.COM",
                "hash", UserRole.User, true, now, now);
            db.Users.Add(author);
            var category = KnowledgeCategory.Create("Grammar", "grammar", null, 0, author.Id, now);
            db.KnowledgeCategories.Add(category);
            await db.SaveChangesAsync();
            return author.Id;
        }

        public async ValueTask DisposeAsync()
        {
            _factory.Dispose();
            var options = new DbContextOptionsBuilder<ToeicDbContext>()
                .UseNpgsql(_connectionString).Options;
            await using var db = new ToeicDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class RecordingAvatarStore : IAvatarStore
    {
        public List<string> DeletedKeys { get; } = [];

        public Task UploadAsync(string objectKey, byte[] content, string contentType,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<AvatarContent> DownloadAsync(string objectKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
        {
            DeletedKeys.Add(objectKey);
            return Task.CompletedTask;
        }
    }
}

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Flashcards;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Tests.Api.Common;

namespace TOEICMaster.Tests.Api.Flashcards;

public sealed class FlashcardsApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Startup:SkipSeeders"] = "true" }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "FlashcardTest";
                    options.DefaultChallengeScheme = "FlashcardTest";
                    options.DefaultForbidScheme = "FlashcardTest";
                })
                .AddScheme<AuthenticationSchemeOptions, FlashcardTestAuthenticationHandler>("FlashcardTest", _ => { });
        });
    }
}

public sealed class FlashcardTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userId) || !Guid.TryParse(userId, out _))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public abstract class FlashcardsPostgresTestBase(FlashcardsApiFactory factory) : IAsyncLifetime
{
    private readonly List<Guid> _userIds = [];
    private readonly List<Guid> _deckIds = [];

    protected FlashcardsApiFactory Factory { get; } = factory;
    protected static DateTimeOffset Now { get; } = new(2026, 11, 25, 8, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_userIds.Count == 0 && _deckIds.Count == 0)
            return;

        await using var db = CreateDbContext();
        await db.FlashcardDecks.Where(deck => _deckIds.Contains(deck.Id)).ExecuteDeleteAsync();
        await db.Users.Where(user => _userIds.Contains(user.Id)).ExecuteDeleteAsync();
    }

    protected ToeicDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var connectionString = Factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Postgres");
        var options = new DbContextOptionsBuilder<ToeicDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.EnableRetryOnFailure(3))
            .AddInterceptors(interceptors)
            .Options;
        return new ToeicDbContext(options);
    }

    protected async Task<Guid> CreateUserAsync()
    {
        var email = $"flashcard-{Guid.NewGuid():N}@toeicmaster.test";
        var user = User.CreateExternal("Flashcard learner", email, email.ToUpperInvariant(), Now);
        _userIds.Add(user.Id);
        await using var db = CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    protected async Task<FlashcardDeck> CreateDeckAsync(Guid? ownerId, int cardCount = 1)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var inputs = Enumerable.Range(1, cardCount).Select(ordinal => CardInput($"term-{ordinal}")).ToArray();
        var deck = ownerId.HasValue
            ? FlashcardDeck.CreateCustom(ownerId.Value, $"Custom {suffix}", "Test vocabulary", inputs, Now)
            : FlashcardDeck.CreateBuiltIn(Guid.NewGuid(), $"built-in-{suffix}", $"Built-in {suffix}",
                "Test vocabulary", FlashcardCategories.Office, "700+", 5,
                inputs.Select(input => (Guid.NewGuid(), input)).ToArray(), Now);
        TrackDeck(deck.Id);
        await using var db = CreateDbContext();
        db.FlashcardDecks.Add(deck);
        await db.SaveChangesAsync();
        return deck;
    }

    protected void TrackDeck(Guid deckId) => _deckIds.Add(deckId);

    protected async Task SeedProgressAsync(Guid userId, Guid cardId, string status = FlashcardMasteryStatus.Learning)
    {
        await using var db = CreateDbContext();
        db.UserFlashcardProgress.Add(UserFlashcardProgress.Create(userId, cardId, status, false, Now));
        await db.SaveChangesAsync();
    }

    protected async Task SeedMatchAsync(Guid userId, Guid deckId, int elapsedMs = 20_000)
    {
        await using var db = CreateDbContext();
        db.UserFlashcardMatchRecords.Add(UserFlashcardMatchRecord.Create(userId, deckId, elapsedMs, 3, Now));
        await db.SaveChangesAsync();
    }

    protected HttpClient CreateUserClient(Guid userId)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
        return client;
    }

    protected static CreateFlashcardCardRequest CardRequest(string term = "negotiate") =>
        new(term, "/nɪˈɡəʊʃieɪt/", "verb", "đàm phán", "We negotiate a contract.",
            "Chúng tôi đàm phán hợp đồng.", ["negotiate a contract"], "Part 5", "B2");

    protected static FlashcardCardInput CardInput(string term = "negotiate")
    {
        var request = CardRequest(term);
        return new FlashcardCardInput(request.Term, request.Phonetic, request.PartOfSpeech, request.MeaningVi,
            request.ExampleEn, request.ExampleVi, request.Collocations, request.ToeicPart, request.Level);
    }
}

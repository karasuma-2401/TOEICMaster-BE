using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.Flashcards;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.Flashcards;

namespace TOEICMaster.Tests.Api.Flashcards;

public sealed class FlashcardsApiTests(FlashcardsApiFactory factory)
    : FlashcardsPostgresTestBase(factory), IClassFixture<FlashcardsApiFactory>
{
    [Fact]
    public async Task Workspace_exposes_built_in_and_owned_decks_and_only_the_current_users_progress()
    {
        var userId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var builtIn = await CreateDeckAsync(null);
        var owned = await CreateDeckAsync(userId);
        var otherDeck = await CreateDeckAsync(otherUserId);
        var cardId = builtIn.Cards.Single().Id;
        await SeedProgressAsync(userId, cardId, FlashcardMasteryStatus.Mastered);
        await SeedProgressAsync(otherUserId, cardId, FlashcardMasteryStatus.Review);
        await SeedMatchAsync(userId, builtIn.Id, 12_000);
        await SeedMatchAsync(otherUserId, builtIn.Id, 15_000);

        using var guest = Factory.CreateClient();
        using var guestResponse = await guest.GetAsync("/api/v1/flashcards/decks");
        Assert.Equal(HttpStatusCode.OK, guestResponse.StatusCode);
        var anonymous = await guestResponse.Content.ReadFromJsonAsync<FlashcardWorkspaceDto>();
        Assert.NotNull(anonymous);
        Assert.Contains(anonymous.Decks, deck => deck.Id == builtIn.Id && !deck.IsCustom);
        Assert.DoesNotContain(anonymous.Decks, deck => deck.Id == owned.Id || deck.Id == otherDeck.Id);
        Assert.Empty(anonymous.Progress);
        Assert.Empty(anonymous.MatchRecords);

        using var client = CreateUserClient(userId);
        using var response = await client.GetAsync("/api/v1/flashcards/decks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var workspace = await response.Content.ReadFromJsonAsync<FlashcardWorkspaceDto>();
        Assert.NotNull(workspace);
        Assert.Contains(workspace.Decks, deck => deck.Id == builtIn.Id);
        Assert.Contains(workspace.Decks, deck => deck.Id == owned.Id && deck.IsCustom);
        Assert.DoesNotContain(workspace.Decks, deck => deck.Id == otherDeck.Id);
        Assert.Equal(FlashcardMasteryStatus.Mastered, workspace.Progress[cardId.ToString()].Status);
        Assert.Equal(12_000, workspace.MatchRecords[builtIn.Id.ToString()].BestTimeMs);
        Assert.Single(workspace.Progress);
        Assert.Single(workspace.MatchRecords);
    }

    [Fact]
    public async Task Create_deck_returns_201_with_a_typed_deck_and_a_resolvable_location()
    {
        var userId = await CreateUserAsync();
        using var client = CreateUserClient(userId);
        var request = new CreateFlashcardDeckRequest($"API deck {Guid.NewGuid():N}", "Personal vocabulary",
            [CardRequest()]);

        using var response = await client.PostAsJsonAsync("/api/v1/flashcards/decks", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var deck = await response.Content.ReadFromJsonAsync<FlashcardDeckDto>();
        Assert.NotNull(deck);
        TrackDeck(deck.Id);
        Assert.True(deck.IsCustom);
        Assert.Equal(request.Title, deck.Title);
        Assert.Equal("negotiate", Assert.Single(deck.Cards).Term);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/v1/flashcards/decks/{deck.Slug}", response.Headers.Location.ToString());
        using var getResponse = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(deck.Id, (await getResponse.Content.ReadFromJsonAsync<FlashcardDeckDto>())?.Id);
        await using var db = CreateDbContext();
        Assert.Equal(userId, (await db.FlashcardDecks.SingleAsync(item => item.Id == deck.Id)).OwnerUserId);
    }

    [Fact]
    public async Task Add_card_returns_201_with_a_card_not_a_deck_wrapper_and_a_resolvable_location()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId);
        using var client = CreateUserClient(userId);

        using var response = await client.PostAsJsonAsync($"/api/v1/flashcards/decks/{deck.Id}/cards",
            CardRequest("  negotiate  "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("negotiate", json.GetProperty("term").GetString());
        Assert.Equal("đàm phán", json.GetProperty("meaningVi").GetString());
        Assert.Equal("B2", json.GetProperty("level").GetString());
        Assert.Equal("negotiate a contract", Assert.Single(json.GetProperty("collocations").EnumerateArray()).GetString());
        Assert.False(json.TryGetProperty("cards", out _));
        Assert.False(json.TryGetProperty("card", out _));
        Assert.False(json.TryGetProperty("deckSlug", out _));
        var cardId = json.GetProperty("id").GetGuid();
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/v1/flashcards/decks/{deck.Slug}", response.Headers.Location.ToString());
        using var getResponse = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var updatedDeck = await getResponse.Content.ReadFromJsonAsync<FlashcardDeckDto>();
        Assert.NotNull(updatedDeck);
        Assert.Equal(2, updatedDeck.Cards.Count);
        Assert.Contains(updatedDeck.Cards, card => card.Id == cardId);
    }

    [Theory]
    [InlineData("Term")]
    [InlineData("MeaningVi")]
    [InlineData("Level")]
    [InlineData("ToeicPart")]
    public async Task Add_card_invalid_input_returns_validation_problem_details_without_a_write(string field)
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId);
        var valid = CardRequest();
        var invalid = field switch
        {
            "Term" => valid with { Term = "" },
            "MeaningVi" => valid with { MeaningVi = "" },
            "Level" => valid with { Level = "A1" },
            "ToeicPart" => valid with { ToeicPart = new string('x', 65) },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        using var client = CreateUserClient(userId);

        using var response = await client.PostAsJsonAsync($"/api/v1/flashcards/decks/{deck.Id}/cards", invalid);

        await AssertValidationProblemAsync(response, field);
        await using var db = CreateDbContext();
        Assert.Equal(1, await db.FlashcardCards.CountAsync(card => card.DeckId == deck.Id));
    }

    [Fact]
    public async Task Add_card_at_the_cap_returns_400_validation_problem_details()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId, FlashcardDeck.MaxCardsPerDeck);
        using var client = CreateUserClient(userId);

        using var response = await client.PostAsJsonAsync($"/api/v1/flashcards/decks/{deck.Id}/cards", CardRequest());

        await AssertValidationProblemAsync(response, "Cards");
        await using var db = CreateDbContext();
        Assert.Equal(200, await db.FlashcardCards.CountAsync(card => card.DeckId == deck.Id));
    }

    [Theory]
    [InlineData("other-user")]
    [InlineData("built-in")]
    [InlineData("missing")]
    public async Task Add_card_to_an_unowned_deck_returns_404(string target)
    {
        var userId = await CreateUserAsync();
        var ownerId = await CreateUserAsync();
        var deck = await CreateDeckAsync(target == "built-in" ? null : ownerId);
        var deckId = target == "missing" ? Guid.NewGuid() : deck.Id;
        using var client = CreateUserClient(userId);

        using var response = await client.PostAsJsonAsync($"/api/v1/flashcards/decks/{deckId}/cards", CardRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var db = CreateDbContext();
        Assert.Equal(1, await db.FlashcardCards.CountAsync(card => card.DeckId == deck.Id));
    }

    [Fact]
    public async Task Progress_returns_a_typed_dto_and_preserves_review_count_for_star_only_toggles()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(null);
        var cardId = deck.Cards.Single().Id;
        using var client = CreateUserClient(userId);

        using var response = await client.PutAsJsonAsync($"/api/v1/flashcards/cards/{cardId}/progress",
            new UpsertFlashcardProgressRequest("LEARNING", false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var progress = await response.Content.ReadFromJsonAsync<CardProgressDto>();
        Assert.NotNull(progress);
        Assert.Equal(FlashcardMasteryStatus.Learning, progress.Status);
        Assert.False(progress.Starred);
        Assert.Equal(1, progress.ReviewCount);
        Assert.Equal(TimeSpan.Zero, progress.UpdatedAt.Offset);
        using var starResponse = await client.PutAsJsonAsync($"/api/v1/flashcards/cards/{cardId}/progress",
            new UpsertFlashcardProgressRequest("learning", true));
        Assert.Equal(HttpStatusCode.OK, starResponse.StatusCode);
        var starred = await starResponse.Content.ReadFromJsonAsync<CardProgressDto>();
        Assert.NotNull(starred);
        Assert.True(starred.Starred);
        Assert.Equal(1, starred.ReviewCount);
        using var reviewResponse = await client.PutAsJsonAsync($"/api/v1/flashcards/cards/{cardId}/progress",
            new UpsertFlashcardProgressRequest("mastered", true));
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var reviewed = await reviewResponse.Content.ReadFromJsonAsync<CardProgressDto>();
        Assert.NotNull(reviewed);
        Assert.Equal(FlashcardMasteryStatus.Mastered, reviewed.Status);
        Assert.Equal(2, reviewed.ReviewCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Progress_for_an_invisible_or_missing_card_returns_404(bool missingCard)
    {
        var userId = await CreateUserAsync();
        var ownerId = await CreateUserAsync();
        var deck = await CreateDeckAsync(ownerId);
        var cardId = missingCard ? Guid.NewGuid() : deck.Cards.Single().Id;
        using var client = CreateUserClient(userId);

        using var response = await client.PutAsJsonAsync($"/api/v1/flashcards/cards/{cardId}/progress",
            new UpsertFlashcardProgressRequest("learning", false));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var db = CreateDbContext();
        Assert.False(await db.UserFlashcardProgress.AnyAsync(progress => progress.UserId == userId));
    }

    [Fact]
    public async Task Progress_returns_404_when_a_deck_is_deleted_after_the_visibility_check()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId);
        var cardId = deck.Cards.Single().Id;
        var gate = new ProgressInsertGateInterceptor();
        using var raceFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<ToeicDbContext>(options => options.AddInterceptors(gate))));
        using var client = raceFactory.CreateClient(new WebApplicationFactoryClientOptions());
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
        var pendingResponse = client.PutAsJsonAsync($"/api/v1/flashcards/cards/{cardId}/progress",
            new UpsertFlashcardProgressRequest("learning", false));

        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await using var db = CreateDbContext();
            Assert.True(await new EfFlashcardRepository(db).DeleteOwnedDeckAsync(userId, deck.Id,
                CancellationToken.None));
        }
        finally
        {
            gate.Continue.TrySetResult();
        }

        using var response = await pendingResponse;
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var verifyDb = CreateDbContext();
        Assert.False(await verifyDb.UserFlashcardProgress.AnyAsync(progress => progress.CardId == cardId));
    }

    [Fact]
    public async Task Match_records_return_200_with_first_faster_slower_and_equal_record_semantics()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(null);
        using var client = CreateUserClient(userId);
        var submissions = new[]
        {
            (18_000, 3, 18_000, 3, true),
            (12_000, 4, 12_000, 4, true),
            (15_000, 8, 12_000, 8, false),
            (12_000, 5, 12_000, 8, false)
        };
        for (var index = 0; index < submissions.Length; index++)
        {
            var (elapsedMs, combo, bestTime, bestCombo, expectedNewRecord) = submissions[index];
            using var response = await client.PostAsJsonAsync($"/api/v1/flashcards/decks/{deck.Id}/match-records",
                new RecordFlashcardMatchRequest(elapsedMs, combo));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var record = await response.Content.ReadFromJsonAsync<MatchRecordDto>();
            Assert.NotNull(record);
            Assert.Equal(expectedNewRecord, record.IsNewRecord);
            Assert.Equal(bestTime, record.BestTimeMs);
            Assert.Equal(bestCombo, record.BestCombo);
            Assert.Equal(index + 1, record.PlayCount);
            Assert.Equal(TimeSpan.Zero, record.UpdatedAt.Offset);
        }
    }

    [Theory]
    [InlineData("progress")]
    [InlineData("match")]
    [InlineData("deck")]
    public async Task Invalid_write_requests_return_validation_problem_details(string operation)
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId);
        using var client = CreateUserClient(userId);
        using var response = operation switch
        {
            "progress" => await client.PutAsJsonAsync($"/api/v1/flashcards/cards/{deck.Cards.Single().Id}/progress",
                new UpsertFlashcardProgressRequest("unknown", false)),
            "match" => await client.PostAsJsonAsync($"/api/v1/flashcards/decks/{deck.Id}/match-records",
                new RecordFlashcardMatchRequest(0, 0)),
            "deck" => await client.PostAsJsonAsync("/api/v1/flashcards/decks",
                new CreateFlashcardDeckRequest("Invalid deck", null, [])),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        var field = operation switch { "progress" => "Status", "match" => "ElapsedMs", _ => "Cards" };
        await AssertValidationProblemAsync(response, field);
    }

    [Theory]
    [InlineData("match", false)]
    [InlineData("match", true)]
    [InlineData("reset", false)]
    [InlineData("reset", true)]
    public async Task Match_and_reset_on_an_invisible_or_missing_deck_return_404(string operation, bool missingDeck)
    {
        var userId = await CreateUserAsync();
        var ownerId = await CreateUserAsync();
        var deck = await CreateDeckAsync(ownerId);
        var deckId = missingDeck ? Guid.NewGuid() : deck.Id;
        using var client = CreateUserClient(userId);
        using var response = operation == "match"
            ? await client.PostAsJsonAsync($"/api/v1/flashcards/decks/{deckId}/match-records",
                new RecordFlashcardMatchRequest(12_000, 4))
            : await client.DeleteAsync($"/api/v1/flashcards/decks/{deckId}/progress");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var db = CreateDbContext();
        Assert.False(await db.UserFlashcardMatchRecords.AnyAsync(record => record.UserId == userId));
        Assert.True(await db.FlashcardDecks.AnyAsync(item => item.Id == deck.Id));
    }

    [Fact]
    public async Task Reset_returns_204_and_only_deletes_the_current_users_progress_in_that_deck()
    {
        var userId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var deck = await CreateDeckAsync(null, 2);
        var otherDeck = await CreateDeckAsync(userId);
        foreach (var card in deck.Cards)
        {
            await SeedProgressAsync(userId, card.Id);
            await SeedProgressAsync(otherUserId, card.Id);
        }
        await SeedProgressAsync(userId, otherDeck.Cards.Single().Id);
        await SeedMatchAsync(userId, deck.Id);
        using var client = CreateUserClient(userId);

        using var response = await client.DeleteAsync($"/api/v1/flashcards/decks/{deck.Id}/progress");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        await using var db = CreateDbContext();
        Assert.Equal(1, await db.UserFlashcardProgress.CountAsync(progress => progress.UserId == userId));
        Assert.Equal(2, await db.UserFlashcardProgress.CountAsync(progress => progress.UserId == otherUserId));
        Assert.Equal(1, await db.UserFlashcardMatchRecords.CountAsync(record => record.UserId == userId));
        using var repeated = await client.DeleteAsync($"/api/v1/flashcards/decks/{deck.Id}/progress");
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
    }

    [Fact]
    public async Task Delete_owned_deck_returns_204_then_404_and_removes_dependent_data()
    {
        var userId = await CreateUserAsync();
        var deck = await CreateDeckAsync(userId);
        var cardId = deck.Cards.Single().Id;
        await SeedProgressAsync(userId, cardId);
        await SeedMatchAsync(userId, deck.Id);
        using var client = CreateUserClient(userId);

        using var response = await client.DeleteAsync($"/api/v1/flashcards/decks/{deck.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        using var repeated = await client.DeleteAsync($"/api/v1/flashcards/decks/{deck.Id}");
        Assert.Equal(HttpStatusCode.NotFound, repeated.StatusCode);
        using var getResponse = await client.GetAsync($"/api/v1/flashcards/decks/{deck.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        await using var db = CreateDbContext();
        Assert.False(await db.FlashcardCards.AnyAsync(card => card.Id == cardId));
        Assert.False(await db.UserFlashcardProgress.AnyAsync(progress => progress.CardId == cardId));
        Assert.False(await db.UserFlashcardMatchRecords.AnyAsync(record => record.DeckId == deck.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_unowned_or_built_in_decks_returns_404_without_deleting_cards(bool builtIn)
    {
        var userId = await CreateUserAsync();
        var ownerId = await CreateUserAsync();
        var deck = await CreateDeckAsync(builtIn ? null : ownerId);
        using var client = CreateUserClient(userId);

        using var response = await client.DeleteAsync($"/api/v1/flashcards/decks/{deck.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var db = CreateDbContext();
        Assert.True(await db.FlashcardDecks.AnyAsync(item => item.Id == deck.Id));
        Assert.Equal(1, await db.FlashcardCards.CountAsync(card => card.DeckId == deck.Id));
    }

    [Theory]
    [InlineData("POST", "decks")]
    [InlineData("POST", "decks/{id}/cards")]
    [InlineData("PUT", "cards/{id}/progress")]
    [InlineData("POST", "decks/{id}/match-records")]
    [InlineData("DELETE", "decks/{id}/progress")]
    [InlineData("DELETE", "decks/{id}")]
    public async Task All_flashcard_write_endpoints_require_authentication(string method, string route)
    {
        using var client = Factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method),
            $"/api/v1/flashcards/{route.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal)}")
        {
            Content = JsonContent.Create(new { })
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, json.GetProperty("status").GetInt32());
        Assert.Contains(json.GetProperty("errors").EnumerateObject(),
            error => error.Name.EndsWith(field, StringComparison.Ordinal) && error.Value.GetArrayLength() > 0);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
    }

    private sealed class ProgressInsertGateInterceptor : DbCommandInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO user_flashcard_progress", StringComparison.Ordinal))
            {
                Reached.TrySetResult();
                await Continue.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }
}

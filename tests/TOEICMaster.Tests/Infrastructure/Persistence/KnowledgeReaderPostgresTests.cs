using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using TOEICMaster.Application.Knowledge;
using TOEICMaster.Application.Knowledge.Admin;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Infrastructure.Persistence.Knowledge;

namespace TOEICMaster.Tests.Infrastructure.Persistence;

public sealed class KnowledgeReaderPostgresTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
    private readonly ReaderCommandCounter _counter = new();
    private readonly string _connectionString;
    private readonly ToeicDbContext _db;

    public KnowledgeReaderPostgresTests()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("TOEICMASTER_TEST_POSTGRES")
            ?? throw new InvalidOperationException("TOEICMASTER_TEST_POSTGRES is required for persistence tests.");
        _connectionString = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Database = $"toeicmaster_knowledge_reader_{Guid.NewGuid():N}",
            Pooling = false
        }.ConnectionString;
        _db = CreateContext(_counter);
    }

    public Task InitializeAsync() => _db.Database.MigrateAsync();

    public async Task DisposeAsync()
    {
        try
        {
            await _db.Database.EnsureDeletedAsync();
        }
        finally
        {
            await _db.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedAsync_hides_url_when_saved_article_is_no_longer_published(bool archive)
    {
        var user = CreateUser();
        var category = KnowledgeCategory.Create("Reading", "reading", null, 0, user.Id, Now);
        var unavailable = CreateArticle("private-lesson-slug", category.Id, [], user.Id, Now);
        var published = CreateArticle("public-lesson", category.Id, [], user.Id, Now);
        _db.Users.Add(user);
        _db.KnowledgeCategories.Add(category);
        _db.KnowledgeArticles.AddRange(unavailable, published);
        await _db.SaveChangesAsync();
        var reader = CreateReader(_db);
        await reader.SaveAsync(user.Id, unavailable.Id, CancellationToken.None);
        await reader.SaveAsync(user.Id, published.Id, CancellationToken.None);

        unavailable.Unpublish(user.Id, Now.AddMinutes(1));
        if (archive)
            unavailable.Archive(user.Id, Now.AddMinutes(2));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var page = await reader.SavedAsync(user.Id, 1, 12, null, CancellationToken.None);
        var saved = page.Items;

        Assert.Equal(2, saved.Count);
        var availableItem = Assert.Single(saved, item => item.ArticleId == published.Id);
        Assert.True(availableItem.Available);
        Assert.Equal(published.Title, availableItem.Title);
        Assert.Equal($"/knowledge/{published.Slug}", availableItem.Url);
        var unavailableItem = Assert.Single(saved, item => item.ArticleId == unavailable.Id);
        Assert.False(unavailableItem.Available);
        Assert.Equal("Lesson is no longer available", unavailableItem.Title);
        Assert.True(string.IsNullOrEmpty(unavailableItem.Url),
            $"An unavailable saved lesson must hide its URL; received '{unavailableItem.Url}'.");
    }

    [Fact]
    public async Task SavedAsync_pages_all_bookmarks_and_can_check_a_bookmark_outside_the_first_page()
    {
        var user = CreateUser();
        var category = KnowledgeCategory.Create("Reading", "reading", null, 0, user.Id, Now);
        var articles = Enumerable.Range(1, 3).Select(i => CreateArticle($"lesson-{i}", category.Id, [], user.Id, Now)).ToArray();
        _db.Users.Add(user);
        _db.KnowledgeCategories.Add(category);
        _db.KnowledgeArticles.AddRange(articles);
        _db.Set<KnowledgeBookmark>().AddRange(articles.Select((a, i) => new KnowledgeBookmark
        { UserId = user.Id, ArticleId = a.Id, SavedAtUtc = Now.AddMinutes(i) }));
        await _db.SaveChangesAsync();
        var reader = CreateReader(_db);
        var first = await reader.SavedAsync(user.Id, 1, 2, null, CancellationToken.None);
        var second = await reader.SavedAsync(user.Id, 2, 2, null, CancellationToken.None);
        Assert.Equal(3, first.TotalCount);
        Assert.True(first.HasNextPage);
        Assert.True(second.HasPreviousPage);
        Assert.Equal(3, first.Items.Concat(second.Items).Select(a => a.ArticleId).Distinct().Count());
        var lookup = await reader.SavedAsync(user.Id, 1, 1, articles[0].Id, CancellationToken.None);
        Assert.Equal(articles[0].Id, Assert.Single(lookup.Items).ArticleId);
        await reader.SaveAsync(user.Id, articles[0].Id, CancellationToken.None);
        Assert.Equal(3, (await reader.SavedAsync(user.Id, 1, 12, null, CancellationToken.None)).TotalCount);
    }

    [Fact]
    public async Task RelatedAsync_returns_three_ranked_published_summaries_with_tags_and_categories_in_one_reader_command()
    {
        var user = CreateUser();
        var reading = KnowledgeCategory.Create("Reading lessons", "reading", null, 0, user.Id, Now);
        var grammar = KnowledgeCategory.Create("Grammar lessons", "grammar", null, 1, user.Id, Now);
        var alpha = KnowledgeTag.Create("Alpha", "alpha", Now);
        var beta = KnowledgeTag.Create("Beta", "beta", Now);
        var current = CreateArticle("current", reading.Id, [alpha.Id, beta.Id], user.Id, Now);
        var mostTags = CreateArticle("most-tags", grammar.Id, [beta.Id, alpha.Id], user.Id, Now.AddDays(-10));
        var sameCategoryNewer = CreateArticle("same-category-newer", reading.Id, [alpha.Id], user.Id, Now.AddDays(-2));
        var sameCategoryOlder = CreateArticle("same-category-older", reading.Id, [beta.Id], user.Id, Now.AddDays(-3));
        var outsideTopThree = CreateArticle("outside-top-three", reading.Id, [alpha.Id], user.Id, Now.AddDays(-4));
        var differentCategoryNewer = CreateArticle("different-category-newer", grammar.Id, [alpha.Id], user.Id, Now.AddDays(-1));
        var categoryOnly = CreateArticle("category-only", reading.Id, [], user.Id, Now);
        var unrelated = CreateArticle("unrelated", grammar.Id, [], user.Id, Now);
        var draft = CreateArticle("draft", reading.Id, [alpha.Id, beta.Id], user.Id, Now);
        draft.Unpublish(user.Id, Now);
        var archived = CreateArticle("archived", reading.Id, [alpha.Id, beta.Id], user.Id, Now);
        archived.Archive(user.Id, Now);
        _db.Users.Add(user);
        _db.KnowledgeCategories.AddRange(reading, grammar);
        _db.KnowledgeTags.AddRange(alpha, beta);
        _db.KnowledgeArticles.AddRange(current, mostTags, sameCategoryNewer, sameCategoryOlder,
            outsideTopThree, differentCategoryNewer, categoryOnly, unrelated, draft, archived);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var reader = CreateReader(_db);
        _counter.Count = 0;

        var related = await reader.RelatedAsync(current.Id, CancellationToken.None);
        var readerCommands = _counter.Count;

        Assert.Equal(new[] { mostTags.Id, sameCategoryNewer.Id, sameCategoryOlder.Id },
            related.Select(item => item.Id));
        var expectedArticles = new[] { mostTags, sameCategoryNewer, sameCategoryOlder };
        for (var i = 0; i < expectedArticles.Length; i++)
        {
            var expected = expectedArticles[i];
            var actual = related[i];
            Assert.Equal("Published", actual.Status);
            Assert.Equal(expected.Title, actual.Title);
            Assert.Equal(expected.Slug, actual.Slug);
            Assert.Equal(expected.Summary, actual.Summary);
            Assert.Equal("Reading", actual.Skill);
            Assert.Equal(expected.EstimatedReadMinutes, actual.EstimatedReadMinutes);
            Assert.Equal(expected.PublishedAtUtc, actual.PublishedAtUtc);
            Assert.Equal(expected.CategoryId, actual.CategoryId);
            Assert.Equal(expected.CategoryId == reading.Id ? reading.Name : grammar.Name, actual.CategoryName);
            var expectedTags = expected.ArticleTags.Select(link => link.TagId).ToHashSet();
            Assert.Equal(expectedTags.Count, actual.Tags.Count);
            Assert.True(expectedTags.SetEquals(actual.Tags.Select(tag => tag.Id)));
            foreach (var tag in actual.Tags)
            {
                var expectedTag = tag.Id == alpha.Id ? alpha : beta;
                Assert.Equal(new KnowledgeTagDto(expectedTag.Id, expectedTag.Name, expectedTag.Slug), tag);
            }
        }
        Assert.Equal(1, readerCommands);
    }

    [Fact]
    public async Task SaveAsync_handles_article_deleted_after_existence_check_without_raw_foreign_key_error()
    {
        var user = CreateUser();
        var category = KnowledgeCategory.Create("Reading", "reading", null, 0, user.Id, Now);
        var article = CreateArticle("delete-during-save", category.Id, [], user.Id, Now);
        _db.Users.Add(user);
        _db.KnowledgeCategories.Add(category);
        _db.KnowledgeArticles.Add(article);
        await _db.SaveChangesAsync();
        var race = new DeleteArticleAfterExistenceCheck(_connectionString, article.Id);
        await using var saveDb = CreateContext(race);
        var reader = CreateReader(saveDb);

        var error = await Record.ExceptionAsync(() => reader.SaveAsync(user.Id, article.Id, CancellationToken.None));

        Assert.True(race.ArticleDeleted, "The article must be deleted between the existence query and bookmark insert.");
        Assert.True(error is null or KnowledgeNotFoundException,
            $"Expected success or KnowledgeNotFoundException; received {error?.GetType().Name}: {error?.Message}");
        Assert.False(await _db.KnowledgeArticles.AnyAsync(item => item.Id == article.Id));
        Assert.False(await _db.Set<KnowledgeBookmark>().AnyAsync(bookmark => bookmark.ArticleId == article.Id));
    }

    private ToeicDbContext CreateContext(DbCommandInterceptor interceptor) => new(
        new DbContextOptionsBuilder<ToeicDbContext>()
            .UseNpgsql(_connectionString)
            .AddInterceptors(interceptor)
            .Options);

    private static EfKnowledgeReader CreateReader(ToeicDbContext db) =>
        new(db, TimeProvider.System);

    private static User CreateUser() =>
        User.CreateExternal("Knowledge learner", "knowledge@example.com", "KNOWLEDGE@EXAMPLE.COM", Now);

    private static KnowledgeArticle CreateArticle(string slug, Guid categoryId, Guid[] tagIds,
        Guid userId, DateTimeOffset publishedAt)
    {
        var article = KnowledgeArticle.Create($"Title {slug}", slug, $"Summary {slug}", KnowledgeSkill.Reading,
            categoryId, null, null, """{"type":"doc","content":[]}""", "[]", 3, tagIds, [], userId, publishedAt);
        article.Publish(userId, publishedAt);
        return article;
    }

    private sealed class ReaderCommandCounter : DbCommandInterceptor
    {
        public int Count { get; set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Count++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class DeleteArticleAfterExistenceCheck(string connectionString, Guid articleId) : DbCommandInterceptor
    {
        public bool ArticleDeleted { get; private set; }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!ArticleDeleted && command.CommandText.Contains("EXISTS", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("knowledge_articles", StringComparison.OrdinalIgnoreCase))
            {
                // The completed SELECT retains its true result while a separate connection removes the article.
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var delete = new NpgsqlCommand("DELETE FROM knowledge_articles WHERE id = @id", connection);
                delete.Parameters.AddWithValue("id", articleId);
                Assert.Equal(1, await delete.ExecuteNonQueryAsync(cancellationToken));
                ArticleDeleted = true;
            }
            return result;
        }
    }
}

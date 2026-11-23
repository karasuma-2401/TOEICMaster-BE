using System.Text.Json;
using TOEICMaster.Application.Knowledge;
using TOEICMaster.Application.Knowledge.Admin;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Knowledge;

public sealed class KnowledgeAdminApplicationTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Slug_generator_normalizes_diacritics_and_punctuation()
    {
        Assert.Equal("toeic-reading-tips", KnowledgeSlug.Generate("TOEIC Reading Tips!!!", 120));
        Assert.Equal("listening-strategy-100", KnowledgeSlug.Generate("  Listening   Strategy 100%  ", 120));
        Assert.Equal("cafe-resume", KnowledgeSlug.Generate("Caf\u00e9 & R\u00e9sum\u00e9", 120));
        Assert.True(KnowledgeSlug.IsValid("reading-tips-2026", 120));
        Assert.False(KnowledgeSlug.IsValid("Invalid Slug!", 120));
    }

    [Fact]
    public void Content_processor_extracts_toc_embedded_media_ids_and_read_time()
    {
        var imageId1 = Guid.NewGuid();
        var imageId2 = Guid.NewGuid();
        var doc = ParseJson($$"""
        {
          "type": "doc",
          "content": [
            {
              "type": "heading",
              "attrs": { "level": 2 },
              "content": [{ "type": "text", "text": "General Introduction" }]
            },
            {
              "type": "paragraph",
              "content": [
                { "type": "text", "text": "Learning TOEIC requires a clear strategy and consistent practice." },
                {
                  "type": "text",
                  "text": "Read more",
                  "marks": [{ "type": "link", "attrs": { "href": "https://example.com/guide" } }]
                }
              ]
            },
            {
              "type": "image",
              "attrs": { "assetId": "{{imageId1}}", "src": "/api/admin/knowledge/media/{{imageId1}}" }
            },
            {
              "type": "heading",
              "attrs": { "level": 3 },
              "content": [{ "type": "text", "text": "General Introduction" }]
            },
            {
              "type": "image",
              "attrs": { "src": "/api/v1/admin/knowledge/media/{{imageId2}}" }
            }
          ]
        }
        """);

        var processed = KnowledgeContentProcessor.Process(doc);

        Assert.Equal(2, processed.Toc.Count);
        Assert.Equal(new KnowledgeTocItemDto("general-introduction", 2, "General Introduction"), processed.Toc[0]);
        Assert.Equal(new KnowledgeTocItemDto("general-introduction-2", 3, "General Introduction"), processed.Toc[1]);
        Assert.Equal(2, processed.EmbeddedMediaAssetIds.Count);
        Assert.Contains(imageId1, processed.EmbeddedMediaAssetIds);
        Assert.Contains(imageId2, processed.EmbeddedMediaAssetIds);
        Assert.Equal(1, processed.EstimatedReadMinutes);
    }

    [Fact]
    public void Content_processor_keeps_heading_ids_unique_when_generated_suffixes_collide()
    {
        var doc = ParseJson("""
        {
          "type": "doc",
          "content": [
            { "type": "heading", "content": [{ "type": "text", "text": "Part" }] },
            { "type": "heading", "content": [{ "type": "text", "text": "Part 2" }] },
            { "type": "heading", "content": [{ "type": "text", "text": "Part" }] },
            { "type": "heading", "content": [{ "type": "text", "text": "Part 3" }] },
            { "type": "heading", "content": [{ "type": "text", "text": "Part" }] }
          ]
        }
        """);

        var processed = KnowledgeContentProcessor.Process(doc);

        Assert.Equal(["part", "part-2", "part-3", "part-3-2", "part-4"], processed.Toc.Select(item => item.Id));
    }

    [Fact]
    public void Content_processor_rejects_unsafe_javascript_links_and_non_doc_root()
    {
        var unsafeLink = ParseJson("""
        {
          "type": "doc",
          "content": [
            {
              "type": "paragraph",
              "content": [
                {
                  "type": "text",
                  "text": "Click",
                  "marks": [{ "type": "link", "attrs": { "href": "javascript:alert(1)" } }]
                }
              ]
            }
          ]
        }
        """);

        Assert.Throws<InvalidKnowledgeContentException>(() => KnowledgeContentProcessor.Process(unsafeLink));
        Assert.Throws<InvalidKnowledgeContentException>(() =>
            KnowledgeContentProcessor.Process(ParseJson("""{"type":"paragraph"}""")));
    }

    [Fact]
    public void Image_signature_validates_png_jpeg_webp_and_rejects_invalid_or_oversized()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0];
        byte[] webp = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

        Assert.Equal("image/png", KnowledgeImageSignature.Validate(png).ContentType);
        Assert.Equal("image/jpeg", KnowledgeImageSignature.Validate(jpeg).ContentType);
        Assert.Equal("image/webp", KnowledgeImageSignature.Validate(webp).ContentType);

        Assert.Throws<InvalidKnowledgeMediaException>(() => KnowledgeImageSignature.Validate("not-an-image"u8.ToArray()));
        Assert.Throws<KnowledgeMediaTooLargeException>(() =>
            KnowledgeImageSignature.Validate(new byte[KnowledgeImageSignature.MaxBytes + 1]));
    }

    [Fact]
    public async Task Delete_category_blocks_with_conflict_when_articles_reference_it()
    {
        var repo = new InMemoryKnowledgeAdminRepository();
        var adminId = Guid.NewGuid();
        var createHandler = new CreateKnowledgeCategoryCommandHandler(repo, new FixedTimeProvider(FixedNow));
        var category = await createHandler.Handle(
            new CreateKnowledgeCategoryCommand(adminId, new KnowledgeCategoryInput("Listening Tips", null, null, 1)),
            CancellationToken.None);

        Assert.Equal("listening-tips", category.Slug);
        repo.CategoriesWithArticles.Add(category.Id);

        var deleteHandler = new DeleteKnowledgeCategoryCommandHandler(repo);
        await Assert.ThrowsAsync<KnowledgeConflictException>(() =>
            deleteHandler.Handle(new DeleteKnowledgeCategoryCommand(category.Id, category.Version),
                CancellationToken.None));

        repo.CategoriesWithArticles.Remove(category.Id);
        await deleteHandler.Handle(new DeleteKnowledgeCategoryCommand(category.Id, category.Version),
            CancellationToken.None);
        Assert.Empty(repo.Categories);
    }

    [Fact]
    public async Task Article_create_update_publish_unpublish_archive_and_delete_follow_business_rules()
    {
        var repo = new InMemoryKnowledgeAdminRepository();
        var clock = new FixedTimeProvider(FixedNow);
        var adminId = Guid.NewGuid();
        var category = KnowledgeCategory.Create("Strategy", "strategy", null, 0, adminId, FixedNow);
        repo.Categories.Add(category);
        var coverAsset = KnowledgeMediaAsset.Create("knowledge/a.png", "cover.png", "image/png", 1024,
            new string('a', 64), adminId, FixedNow);
        repo.MediaAssets.Add(coverAsset);

        var createHandler = new CreateKnowledgeArticleCommandHandler(repo, clock);
        var created = await createHandler.Handle(new CreateKnowledgeArticleCommand(adminId, new KnowledgeArticleInput(
            "Reading Test Strategies",
            null,
            "Guide to effective time allocation.",
            "Reading",
            category.Id,
            coverAsset.Id,
            "Reading cover image",
            ParseJson("""{"type":"doc","content":[{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Step 1"}]}]}"""),
            ["Reading", "Exam Tips"])), CancellationToken.None);

        Assert.Equal("reading-test-strategies", created.Slug);
        Assert.Equal("Draft", created.Status);
        Assert.Single(created.Toc);
        Assert.Equal(2, created.Tags.Count);

        var publishHandler = new PublishKnowledgeArticleCommandHandler(repo, clock);
        var published = await publishHandler.Handle(
            new PublishKnowledgeArticleCommand(adminId, created.Id, created.Version), CancellationToken.None);
        Assert.Equal("Published", published.Status);
        Assert.NotNull(published.PublishedAtUtc);

        // Editing a published article is allowed and keeps Published status.
        var updateHandler = new UpdateKnowledgeArticleCommandHandler(repo, clock);
        var updated = await updateHandler.Handle(new UpdateKnowledgeArticleCommand(adminId, published.Id,
            published.Version, new KnowledgeArticleInput(
                "Reading Test Strategies (Updated)",
                published.Slug,
                "Latest update.",
                "Reading",
                category.Id,
                coverAsset.Id,
                "Reading cover image",
                ParseJson("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"New content"}]}]}"""),
                ["Reading"])), CancellationToken.None);
        Assert.Equal("Published", updated.Status);
        Assert.Single(updated.Tags);

        // Deleting a Published article is blocked with 409 Conflict.
        var deleteHandler = new DeleteKnowledgeArticleCommandHandler(repo);
        await Assert.ThrowsAsync<KnowledgeConflictException>(() =>
            deleteHandler.Handle(new DeleteKnowledgeArticleCommand(updated.Id, updated.Version),
                CancellationToken.None));

        // Unpublish -> Draft, then Delete succeeds.
        var unpublishHandler = new UnpublishKnowledgeArticleCommandHandler(repo, clock);
        var unpublished = await unpublishHandler.Handle(
            new UnpublishKnowledgeArticleCommand(adminId, updated.Id, updated.Version), CancellationToken.None);
        Assert.Equal("Draft", unpublished.Status);

        await deleteHandler.Handle(new DeleteKnowledgeArticleCommand(unpublished.Id, unpublished.Version),
            CancellationToken.None);
        Assert.Empty(repo.Articles);
    }

    [Fact]
    public async Task Delete_media_asset_blocks_when_used_by_article_and_deletes_when_unused()
    {
        var repo = new InMemoryKnowledgeAdminRepository();
        var store = new InMemoryKnowledgeMediaStore();
        var adminId = Guid.NewGuid();
        var category = KnowledgeCategory.Create("Tips", "tips", null, 0, adminId, FixedNow);
        repo.Categories.Add(category);

        var asset = KnowledgeMediaAsset.Create("knowledge/img1.png", "img1.png", "image/png", 512,
            new string('b', 64), adminId, FixedNow);
        repo.MediaAssets.Add(asset);
        store.Objects[asset.ObjectKey] = ([0x89, 0x50, 0x4E, 0x47], "image/png");

        var article = KnowledgeArticle.Create("Article", "article", "Summary", KnowledgeSkill.General,
            category.Id, asset.Id, null, """{"type":"doc","content":[]}""", "[]", 1, [], [], adminId, FixedNow);
        repo.Articles.Add(article);

        var deleteMediaHandler = new DeleteKnowledgeMediaAssetCommandHandler(repo, store);
        await Assert.ThrowsAsync<KnowledgeConflictException>(() =>
            deleteMediaHandler.Handle(new DeleteKnowledgeMediaAssetCommand(asset.Id), CancellationToken.None));
        Assert.Single(repo.MediaAssets);
        Assert.True(store.Objects.ContainsKey(asset.ObjectKey));

        // Once no article uses the image, deletion succeeds and cleans up storage.
        repo.Articles.Clear();
        await deleteMediaHandler.Handle(new DeleteKnowledgeMediaAssetCommand(asset.Id), CancellationToken.None);
        Assert.Empty(repo.MediaAssets);
        Assert.False(store.Objects.ContainsKey(asset.ObjectKey));
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

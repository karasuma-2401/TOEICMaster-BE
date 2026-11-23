using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Admin;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Speaking;

public sealed class SpeakingTestContentRulesTests
{
    [Theory]
    [InlineData(" What  TIME is it?! ", "what time is it")]
    [InlineData("Ｈｅｌｌｏ！", "hello")]
    [InlineData("café", "cafe\u0301")]
    public void Identity_ignores_case_punctuation_spacing_and_compatibility_forms(string left, string right) =>
        Assert.Equal(SpeakingTestContentRules.Normalize(left), SpeakingTestContentRules.Normalize(right));

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Exercise_validation_rejects_duplicate_questions(int part)
    {
        var result = new SpeakingExerciseInputValidator().Validate(SpeakingSamples.Exercise(part, "SP-X") with
        { Questions = ["What time is it?", "WHAT TIME IS IT!", "Where is it?"] });
        Assert.Contains(result.Errors, item => item.PropertyName == "Questions");
    }

    [Fact]
    public void Content_check_reports_all_duplicate_pairs_across_parts()
    {
        var errors = SpeakingTestContentRules.Check([
            (1, 1, new SpeakingContentDto(1, Passage: "Read this!")),
            (1, 2, new SpeakingContentDto(1, Passage: "read this")),
            (3, 1, new SpeakingContentDto(3, Questions: ["When?", "Where?", "Why?"])),
            (4, 1, new SpeakingContentDto(4, Questions: ["WHEN!", "How?", "Who?"]))
        ], new Dictionary<Guid, SpeakingMediaAsset>());
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, message => message.Contains("Question 1 and Question 2", StringComparison.Ordinal));
        Assert.Contains(errors, message => message.Contains("Question 5 and Question 8", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Images_match_by_id_or_by_bytes(bool sameId)
    {
        var first = SpeakingMediaAsset.Create(SpeakingMediaKind.Image, "one", "image/png", 1, "same-hash",
            Guid.NewGuid(), DateTimeOffset.UtcNow);
        var second = SpeakingMediaAsset.Create(SpeakingMediaKind.Image, "two", "image/png", 1, "same-hash",
            Guid.NewGuid(), DateTimeOffset.UtcNow);
        var images = new Dictionary<Guid, SpeakingMediaAsset> { [first.Id] = first, [second.Id] = second };
        var errors = SpeakingTestContentRules.Check([
            (2, 1, new SpeakingContentDto(2, ImageAssetId: first.Id)),
            (2, 2, new SpeakingContentDto(2, ImageAssetId: sameId ? first.Id : second.Id))
        ], images);
        Assert.Single(errors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_rechecks_content_in_both_authoring_modes(bool direct)
    {
        var store = new InMemorySpeaking();
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var actor = Guid.NewGuid();
        var imageHandler = new UploadSpeakingImageCommandHandler(store, store, clock);
        var images = new[] {
            await imageHandler.Handle(new(actor, SpeakingSamples.PngVariant(1), "image/png"), CancellationToken.None),
            await imageHandler.Handle(new(actor, SpeakingSamples.PngVariant(2), "image/png"), CancellationToken.None)
        };
        var test = SpeakingTest.Create("SP-DUP", "Duplicate", "", direct ? SpeakingTestContentMode.Direct :
            SpeakingTestContentMode.ExerciseBank, actor, clock.GetUtcNow());
        var slots = new List<SpeakingTestExercise>();
        foreach (var part in Enumerable.Range(1, 5))
            for (var sequence = 1; sequence <= SpeakingTiming.SlotsPerPart(part); sequence++)
            {
                var input = SpeakingSamples.Exercise(part, $"SP{part}-{sequence}", images[sequence == 2 ? 1 : 0].Id);
                if (part == 1) input = input with { Passage = "Same passage." };
                var exercise = await new CreateSpeakingExerciseCommandHandler(store, clock).Handle(new(actor, input), CancellationToken.None);
                if (direct)
                {
                    var content = new SpeakingTestQuestionContentDto(exercise.RubricNotes, exercise.ModelAnswer,
                        exercise.Passage, exercise.ImageAssetId, exercise.ImageAlt, exercise.Context, exercise.Questions,
                        exercise.DocumentTitle, exercise.DocumentRows, exercise.CallerIntro, exercise.Prompt);
                    slots.Add(SpeakingTestExercise.CreateDirect(test.Id, part, sequence, SpeakingJson.Serialize(content), exercise.ImageAssetId));
                }
                else slots.Add(SpeakingTestExercise.Create(test.Id, part, sequence, exercise.Id));
            }
        store.AddTest(test, slots);
        var error = await Assert.ThrowsAsync<SpeakingPublishRequirementsException>(() =>
            new PublishSpeakingTestCommandHandler(store, clock).Handle(new(actor, test.Id, test.Version), CancellationToken.None));
        Assert.Contains("Content", error.Errors.Keys);
        Assert.True(test.IsDraft);
        Assert.All(slots, slot => Assert.Null(slot.SnapshotJson));
    }

    [Fact]
    public async Task Clone_copies_published_content_into_an_independent_direct_draft()
    {
        var store = new InMemorySpeaking();
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var source = await SpeakingAdministrationTests.CreatePublishedTestAsync(store, clock);
        var actor = Guid.NewGuid();
        var handler = new CloneSpeakingTestCommandHandler(store, clock);
        var cloned = await handler.Handle(new(actor, source.Id, source.Version, "SP-COPY", "Copy"), CancellationToken.None);
        Assert.NotEqual(source.Id, cloned.Id);
        Assert.Equal(("DRAFT", "DIRECT", 7), (cloned.Status, cloned.ContentMode, cloned.Questions.Count));
        Assert.Equal(source.Exercises[0].Exercise.Passage, cloned.Questions[0].Content.Passage);
        Assert.Equal(SpeakingTestStatus.Published, store.Tests.Single(test => test.Id == source.Id).Status);
        await Assert.ThrowsAsync<SpeakingConflictException>(() => handler.Handle(new(actor, source.Id,
            source.Version + 1, "SP-COPY-2", "Copy"), CancellationToken.None));
        await Assert.ThrowsAsync<SpeakingConflictException>(() => handler.Handle(new(actor, source.Id,
            source.Version, "SP-COPY", "Copy"), CancellationToken.None));
        Assert.Equal(2, store.Tests.Count);
    }
}

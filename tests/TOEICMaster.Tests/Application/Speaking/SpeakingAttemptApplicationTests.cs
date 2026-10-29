using System.Text.Json;
using FluentValidation;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Admin;
using TOEICMaster.Application.Speaking.Attempts;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Speaking;

public sealed class SpeakingAttemptApplicationTests
{
    private static readonly SpeakingAttemptOwner Learner = SpeakingAttemptOwner.ForUser(Guid.NewGuid());
    private static readonly SpeakingAttemptOwner Guest =
        SpeakingAttemptOwner.ForAnonymousSession("6f9619ff-8b86-d011-b42d-00c04fc964ff");
    private readonly InMemorySpeaking _store = new();
    private readonly InMemorySpeakingEvaluations _evaluations;
    private readonly MutableTimeProvider _clock = new(new DateTimeOffset(2026, 10, 23, 10, 0, 0, TimeSpan.Zero));

    public SpeakingAttemptApplicationTests() => _evaluations = new InMemorySpeakingEvaluations(_store);

    [Fact]
    public async Task Published_tests_expose_standard_part_durations_without_review_material()
    {
        var test = await PublishAsync();

        var page = await new GetPublishedSpeakingTestsQueryHandler(_store).Handle(
            new GetPublishedSpeakingTestsQuery(null), CancellationToken.None);
        var publicTest = page.Items.Single();

        Assert.Equal(test.Id, publicTest.Id);
        Assert.Equal([(1, 2, 180), (2, 2, 150), (3, 3, 69), (4, 3, 114), (5, 1, 105)],
            publicTest.Parts.Select(part => (part.Part, part.QuestionCount, part.DurationSeconds)));
        var json = JsonSerializer.Serialize(publicTest, SpeakingJson.Options);
        Assert.DoesNotContain("rubric", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("modelAnswer", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_full_attempt_expands_part_three_and_four_into_three_questions_each()
    {
        var test = await PublishAsync();

        var attempt = await StartAsync(test.Id, "FULL");

        Assert.Equal(("FULL", "IN_PROGRESS", 1, "PREPARING"), (attempt.Mode, attempt.Status,
            attempt.CurrentQuestionPosition, attempt.CurrentPhase));
        Assert.Equal(_clock.Now.AddSeconds(45), attempt.PhaseEndsAtUtc);
        Assert.Equal(Enumerable.Range(1, 11), attempt.Questions.Select(question => question.Position));
        Assert.Equal([1, 1, 2, 2, 3, 3, 3, 4, 4, 4, 5], attempt.Questions.Select(question => question.Part));
        Assert.Equal([0, 0, 0, 0, 0, 1, 2, 0, 1, 2, 0], attempt.Questions.Select(question => question.QuestionIndex));
        Assert.All(attempt.Questions, question => Assert.Equal(question.Part, question.Content.Part));
        Assert.Empty(attempt.ReviewQuestions);
        Assert.Equal(("SPK-01", "TOEIC Speaking Practice Test 01"), (attempt.TestCode, attempt.TestTitle));
    }

    [Fact]
    public async Task Part_modes_renumber_from_one_and_reject_missing_parts()
    {
        var test = await PublishAsync();

        var attempt = await StartAsync(test.Id, "part_2");

        Assert.Equal("PART_2", attempt.Mode);
        Assert.Equal([1, 2], attempt.Questions.Select(question => question.Position));
        Assert.All(attempt.Questions, question => Assert.Equal(2, question.Part));
        Assert.NotNull(attempt.Questions[0].Content.ImageAssetId);
        await Assert.ThrowsAsync<ValidationException>(() => StartAsync(test.Id, "PART_6"));
        await Assert.ThrowsAsync<SpeakingNotFoundException>(() => StartAsync(Guid.NewGuid(), "FULL"));
    }

    [Fact]
    public async Task Unreached_part_three_and_four_questions_are_hidden()
    {
        var test = await PublishAsync();
        var part3 = await StartAsync(test.Id, "PART_3");
        Assert.All(part3.Questions, question =>
            Assert.Equal(["How often do you read?", null, null], question.Content.Questions!));

        var part4 = await StartAsync(test.Id, "PART_4");
        Assert.Equal("READING", part4.CurrentPhase);
        Assert.All(part4.Questions, question => Assert.Equal([null, null, null], question.Content.Questions!));
        Assert.Equal("Marketing Conference · May 14", part4.Questions[0].Content.DocumentTitle);

        var preparing = await NextAsync(part4.Id);
        Assert.Equal(("PREPARING", "Where is it held?"), (preparing.CurrentPhase, preparing.Questions[0].Content.Questions![0]));
        Assert.Null(preparing.Questions[0].Content.Questions![1]);

        _clock.Advance(3 + 15 + 3);
        var second = await GetAsync(part4.Id);
        Assert.Equal((2, "RESPONDING"), (second.CurrentQuestionPosition, second.CurrentPhase));
        Assert.Equal(["Where is it held?", "Is lunch in the main hall?", null], second.Questions[2].Content.Questions!);
    }

    [Fact]
    public async Task Reading_an_attempt_advances_every_expired_deadline_and_reveals_the_review()
    {
        var test = await PublishAsync();
        var attempt = await StartAsync(test.Id, "PART_3");
        _clock.Advance(3600);

        var submitted = await GetAsync(attempt.Id);

        Assert.Equal(("SUBMITTED", null, null, null), (submitted.Status, submitted.CurrentQuestionPosition,
            submitted.CurrentPhase, submitted.PhaseEndsAtUtc));
        Assert.Equal(attempt.CreatedAtUtc.AddSeconds(69), submitted.SubmittedAtUtc);
        Assert.All(submitted.Questions, question => Assert.NotNull(question.LockedAtUtc));
        Assert.Equal(["How often do you read?", "Printed or e-books?", "Recommend a book."],
            submitted.Questions[0].Content.Questions!);
        Assert.Equal(3, submitted.ReviewQuestions.Count);
        Assert.All(submitted.ReviewQuestions, review =>
            Assert.Equal(("Rõ ràng, đúng trọng tâm.", "Model answer SP3-001."), (review.RubricNotes, review.ModelAnswer)));
    }

    [Fact]
    public async Task Next_on_the_last_response_submits_and_submit_is_idempotent()
    {
        var test = await PublishAsync();
        var attempt = await StartAsync(test.Id, "PART_5");
        await NextAsync(attempt.Id);

        var submitted = await NextAsync(attempt.Id);
        Assert.Equal("SUBMITTED", submitted.Status);
        var again = await new SubmitSpeakingAttemptCommandHandler(_store, _clock).Handle(
            new SubmitSpeakingAttemptCommand(Learner, attempt.Id), CancellationToken.None);
        Assert.Equal(submitted.SubmittedAtUtc, again.SubmittedAtUtc);
    }

    [Fact]
    public async Task Attempts_are_invisible_to_other_owners()
    {
        var test = await PublishAsync();
        var attempt = await StartAsync(test.Id, "PART_5");
        var guestAttempt = await new StartSpeakingAttemptCommandHandler(_store, _clock).Handle(
            new StartSpeakingAttemptCommand(Guest, new StartSpeakingAttemptInput(test.Id, "PART_1")),
            CancellationToken.None);

        await Assert.ThrowsAsync<SpeakingNotFoundException>(() => new GetSpeakingAttemptQueryHandler(_store, _evaluations, _clock)
            .Handle(new GetSpeakingAttemptQuery(SpeakingAttemptOwner.ForUser(Guid.NewGuid()), attempt.Id),
                CancellationToken.None));
        await Assert.ThrowsAsync<SpeakingNotFoundException>(() => new GetSpeakingAttemptQueryHandler(_store, _evaluations, _clock)
            .Handle(new GetSpeakingAttemptQuery(SpeakingAttemptOwner.ForAnonymousSession(Guid.NewGuid().ToString()),
                guestAttempt.Id), CancellationToken.None));
        var history = await new GetSpeakingAttemptsQueryHandler(_store, _evaluations).Handle(new GetSpeakingAttemptsQuery(Guest),
            CancellationToken.None);
        Assert.Equal([guestAttempt.Id], history.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Recordings_are_validated_stored_and_replaced_within_the_grace_period()
    {
        var test = await PublishAsync();
        var attempt = await StartAsync(test.Id, "PART_5");

        await Assert.ThrowsAsync<SpeakingConflictException>(() => UploadAsync(attempt.Id, 1, SpeakingSamples.WebmAudio));
        await NextAsync(attempt.Id);
        await Assert.ThrowsAsync<InvalidSpeakingMediaException>(() =>
            UploadAsync(attempt.Id, 1, SpeakingSamples.Png, "audio/webm"));
        await Assert.ThrowsAsync<SpeakingMediaTooLargeException>(() =>
            UploadAsync(attempt.Id, 1, new byte[60 * 32 * 1024 + 256 * 1024 + 1]));
        await Assert.ThrowsAsync<ValidationException>(() =>
            UploadAsync(attempt.Id, 1, SpeakingSamples.WebmAudio, durationSeconds: 63));

        var uploaded = await UploadAsync(attempt.Id, 1, SpeakingSamples.WebmAudio, "audio/webm;codecs=opus");
        Assert.Equal(14, uploaded.Questions[0].Recording!.DurationSeconds);
        var firstKey = Recordings().Single().ObjectKey;

        await NextAsync(attempt.Id);
        _clock.Advance(15);
        var replaced = await UploadAsync(attempt.Id, 1, SpeakingSamples.WebmAudio, durationSeconds: 15);
        Assert.Equal(15, replaced.Questions[0].Recording!.DurationSeconds);
        Assert.DoesNotContain((SpeakingMediaKind.Recording, firstKey), _store.Objects.Keys);
        var asset = Assert.Single(Recordings());
        Assert.Equal(("audio/webm", Learner.UserId), (asset.ContentType, asset.CreatedByUserId));

        var audio = await new GetSpeakingRecordingQueryHandler(_store, _store).Handle(
            new GetSpeakingRecordingQuery(Learner, attempt.Id, 1), CancellationToken.None);
        Assert.Equal(("audio/webm", SpeakingSamples.WebmAudio), (audio.ContentType, audio.Data));

        _clock.Advance(1);
        await Assert.ThrowsAsync<SpeakingConflictException>(() => UploadAsync(attempt.Id, 1, SpeakingSamples.WebmAudio));
        Assert.Single(_store.Objects.Keys, key => key.Kind == SpeakingMediaKind.Recording);
    }

    [Fact]
    public async Task Concurrent_next_is_rejected_while_reads_and_uploads_recover()
    {
        var test = await PublishAsync();
        var attempt = await StartAsync(test.Id, "PART_5");

        _store.PendingConcurrencyConflicts = 1;
        await Assert.ThrowsAsync<SpeakingConcurrencyException>(() => NextAsync(attempt.Id));

        // The in-memory store keeps the change, standing in for the request that saved first (now RESPONDING).
        _store.PendingConcurrencyConflicts = 2;
        var uploaded = await UploadAsync(attempt.Id, 1, SpeakingSamples.WebmAudio);
        Assert.NotNull(uploaded.Questions[0].Recording);
        Assert.Single(_store.Objects.Keys, key => key.Kind == SpeakingMediaKind.Recording);

        _clock.Advance(70);
        _store.PendingConcurrencyConflicts = 1;
        Assert.Equal("SUBMITTED", (await GetAsync(attempt.Id)).Status);
    }

    [Fact]
    public async Task Published_images_are_served_only_through_a_published_snapshot()
    {
        var test = await PublishAsync();
        var imageId = _store.Assets.Single(asset => asset.Kind == SpeakingMediaKind.Image).Id;
        var handler = new GetPublishedSpeakingImageQueryHandler(_store, _store);

        var image = await handler.Handle(new GetPublishedSpeakingImageQuery(imageId), CancellationToken.None);

        Assert.Equal("image/png", image.ContentType);
        await Assert.ThrowsAsync<SpeakingNotFoundException>(() =>
            handler.Handle(new GetPublishedSpeakingImageQuery(Guid.NewGuid()), CancellationToken.None));
        Assert.NotEqual(Guid.Empty, test.Id);
    }

    private IEnumerable<SpeakingMediaAsset> Recordings() =>
        _store.Assets.Where(asset => asset.Kind == SpeakingMediaKind.Recording);

    private Task<SpeakingTestDetailDto> PublishAsync() =>
        SpeakingAdministrationTests.CreatePublishedTestAsync(_store, _clock);

    private async Task<SpeakingAttemptDto> StartAsync(Guid testId, string mode)
    {
        var command = new StartSpeakingAttemptCommand(Learner, new StartSpeakingAttemptInput(testId, mode));
        await new StartSpeakingAttemptCommandValidator().ValidateAndThrowAsync(command);
        return await new StartSpeakingAttemptCommandHandler(_store, _clock).Handle(command, CancellationToken.None);
    }

    private Task<SpeakingAttemptDto> GetAsync(Guid attemptId) =>
        new GetSpeakingAttemptQueryHandler(_store, _evaluations, _clock).Handle(new GetSpeakingAttemptQuery(Learner, attemptId),
            CancellationToken.None);

    private Task<SpeakingAttemptDto> NextAsync(Guid attemptId) =>
        new NextSpeakingPhaseCommandHandler(_store, _clock).Handle(new NextSpeakingPhaseCommand(Learner, attemptId),
            CancellationToken.None);

    private Task<SpeakingAttemptDto> UploadAsync(Guid attemptId, int position, byte[] content,
        string contentType = "audio/webm", int durationSeconds = 14) =>
        new UploadSpeakingRecordingCommandHandler(_store, _store, _clock).Handle(
            new UploadSpeakingRecordingCommand(Learner, attemptId, position, content, contentType, durationSeconds),
            CancellationToken.None);
}

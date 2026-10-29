using FluentValidation;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Attempts;
using TOEICMaster.Application.Speaking.Evaluations;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Speaking;

public sealed class SpeakingEvaluationApplicationTests
{
    private static readonly SpeakingAttemptOwner Learner = SpeakingAttemptOwner.ForUser(Guid.NewGuid());
    private static readonly SpeakingAttemptOwner Guest =
        SpeakingAttemptOwner.ForAnonymousSession("6f9619ff-8b86-d011-b42d-00c04fc964ff");
    private readonly InMemorySpeaking _store = new();
    private readonly InMemorySpeakingEvaluations _evaluations;
    private readonly StaticSpeakingEvaluationSettings _settings = new();
    private readonly FakeSpeechAssessor _speech = new();
    private readonly FakeLanguageEvaluator _language = new();
    private readonly MutableTimeProvider _clock = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));
    private Guid? _testId;

    public SpeakingEvaluationApplicationTests() => _evaluations = new InMemorySpeakingEvaluations(_store);

    [Fact]
    public async Task Consent_is_accepted_only_after_submission_once_every_upload_window_closed()
    {
        var inProgress = await StartAsync(Learner, "PART_5");
        await Assert.ThrowsAsync<SpeakingConflictException>(() => ConsentAsync(Learner, inProgress));

        var attemptId = await RecordAndSubmitAsync(Learner, "PART_5", closeUploadWindow: false);
        await Assert.ThrowsAsync<SpeakingConflictException>(() => ConsentAsync(Learner, attemptId));

        _clock.Advance(16);
        var queued = await ConsentAsync(Learner, attemptId);
        Assert.Equal(("QUEUED", "azure-speech", "gemini", "toeic-speaking-v1"), (queued.Evaluation!.Status,
            queued.Evaluation.SpeechProvider, queued.Evaluation.LanguageProvider, queued.Evaluation.RubricVersion));
        Assert.Empty(queued.Evaluation.Items);
        Assert.Equal(_clock.Now, queued.Evaluation.ConsentAcceptedAtUtc);

        var again = await ConsentAsync(Learner, attemptId);
        Assert.Equal("QUEUED", again.Evaluation!.Status);
        Assert.Single(_evaluations.Evaluations);
    }

    [Fact]
    public async Task Consent_requires_agreement_configuration_recordings_and_the_owner()
    {
        var validator = new GrantSpeakingEvaluationConsentCommandValidator();
        var refused = await validator.ValidateAsync(new GrantSpeakingEvaluationConsentCommand(Learner, Guid.NewGuid(),
            new GrantSpeakingEvaluationConsentInput(false)));
        Assert.Contains(refused.Errors, error => error.PropertyName == "Accepted");

        var silent = await RecordAndSubmitAsync(Learner, "PART_5", recorded: []);
        await Assert.ThrowsAsync<SpeakingConflictException>(() => ConsentAsync(Learner, silent));

        var recorded = await RecordAndSubmitAsync(Learner, "PART_5");
        await Assert.ThrowsAsync<SpeakingNotFoundException>(() =>
            ConsentAsync(SpeakingAttemptOwner.ForUser(Guid.NewGuid()), recorded));
        _settings.IsConfigured = false;
        await Assert.ThrowsAsync<SpeakingConflictException>(() => ConsentAsync(Learner, recorded));
        Assert.Empty(_evaluations.Evaluations);
    }

    [Fact]
    public async Task A_guest_session_gets_one_ai_evaluation_per_day()
    {
        var first = await RecordAndSubmitAsync(Guest, "PART_5");
        var second = await RecordAndSubmitAsync(Guest, "PART_5");

        await ConsentAsync(Guest, first);
        await Assert.ThrowsAsync<SpeakingConflictException>(() => ConsentAsync(Guest, second));

        _clock.Advance(24 * 60 * 60 + 1);
        Assert.Equal("QUEUED", (await ConsentAsync(Guest, second)).Evaluation!.Status);
    }

    [Fact]
    public async Task A_full_test_combines_azure_pronunciation_with_language_model_scores()
    {
        _speech.Result = audio => audio[^1] switch
        {
            1 or 2 => FakeSpeechAssessor.Recognized("Attention, shoppers. The store closes in 30 minutes.",
                lexical: "attention shoppers the store closes in thirty minutes"),
            11 => new SpeakingSpeechResult(SpeakingRecognitionStatus.NoSpeech, "", "", 60, 0, 0, null, 0, []),
            _ => FakeSpeechAssessor.Recognized("I usually read in the evening.")
        };
        var attemptId = await RecordAndSubmitAsync(Learner, "FULL",
            recorded: [1, 2, 4, 5, 6, 7, 8, 9, 10, 11]);
        await ConsentAsync(Learner, attemptId);

        Assert.True(await ProcessAsync());

        var evaluation = (await GetAsync(Learner, attemptId)).Evaluation!;
        Assert.Equal("COMPLETED", evaluation.Status);
        var call = Assert.Single(_language.Calls);
        Assert.Equal([1, 2, 4, 5, 6, 7, 8, 9, 10], call.Items.Select(item => item.Position));
        Assert.Equal([0, 1, 2], call.Items.Where(item => item.Part == 3).Select(item => item.QuestionIndex));

        var items = evaluation.Items.ToDictionary(item => item.Position);
        Assert.Equal(Enumerable.Range(1, 11), items.Keys);
        // Part 1: 0.7 × pronunciation 80 + 0.3 × completeness 100 = 86 → 3/3; no grammar or vocabulary.
        Assert.Equal((100d, (int?)null, (int?)null, 3, 3), (items[1].CompletenessScore!.Value,
            items[1].GrammarScore, items[1].VocabularyScore, items[1].RawScore, items[1].MaxRawScore));
        // Part 2-4: every criterion 80 → 2.4 → 2/3, with the language scores kept.
        Assert.Equal((80, 80, 80, 2), (items[4].GrammarScore, items[4].VocabularyScore, items[4].RelevanceScore,
            items[4].RawScore));
        Assert.Equal(("NO_RECORDING", 0), (items[3].RecognitionStatus, items[3].RawScore));
        Assert.Equal(("NO_SPEECH", 0, 5), (items[11].RecognitionStatus, items[11].RawScore, items[11].MaxRawScore));
        Assert.Equal([new SpeakingWordIssueDto("table", 41)], items[5].WordIssues);
        Assert.Equal("Better answer.", items[5].SuggestedResponse);
        Assert.Null(items[1].SuggestedResponse);
        // Raw 3+3+0+2+2·3+2·3+0 = 20 of 35 → 20 × 20 / 35 = 11.4 → 110.
        Assert.Equal(110, evaluation.EstimatedScore);
        Assert.Equal("Bài nói khá tốt.", evaluation.Summary);
        Assert.Equal(["Phát âm rõ."], evaluation.Strengths);

        var history = await new GetSpeakingAttemptsQueryHandler(_store, _evaluations).Handle(
            new GetSpeakingAttemptsQuery(Learner), CancellationToken.None);
        Assert.Equal(("COMPLETED", 110), (history.Items.Single().EvaluationStatus, history.Items.Single().EstimatedScore));
    }

    [Fact]
    public async Task An_off_topic_answer_cannot_earn_more_than_one_point()
    {
        _speech.Result = _ => FakeSpeechAssessor.Recognized("I like football.", pronunciation: 100);
        _language.Score = 100;
        _language.Relevance = SpeakingEvaluationScoring.RelevanceGate - 1;
        var attemptId = await RecordAndSubmitAsync(Learner, "PART_5");
        await ConsentAsync(Learner, attemptId);

        await ProcessAsync();

        var evaluation = (await GetAsync(Learner, attemptId)).Evaluation!;
        Assert.Equal((1, 5), (evaluation.Items.Single().RawScore, evaluation.Items.Single().MaxRawScore));
        Assert.Equal(40, evaluation.EstimatedScore);
    }

    [Fact]
    public async Task A_language_model_outage_is_retried_without_transcribing_again()
    {
        var attemptId = await RecordAndSubmitAsync(Learner, "PART_3");
        await ConsentAsync(Learner, attemptId);
        _language.Failure = new SpeakingEvaluatorException("QUOTA", "Dịch vụ chấm AI đang bận.", true);

        await ProcessAsync();

        var retrying = (await GetAsync(Learner, attemptId)).Evaluation!;
        Assert.Equal(("RETRYING", 1, "Dịch vụ chấm AI đang bận."), (retrying.Status, retrying.RetryCount,
            retrying.FailureMessage));
        Assert.Equal(3, _speech.Calls);
        Assert.False(await ProcessAsync(), "the retry waits for its back-off delay");

        _language.Failure = null;
        _clock.Advance(60);
        await ProcessAsync();

        Assert.Equal("COMPLETED", (await GetAsync(Learner, attemptId)).Evaluation!.Status);
        Assert.Equal(3, _speech.Calls);
    }

    [Fact]
    public async Task A_permanent_failure_stops_until_the_learner_asks_again()
    {
        var attemptId = await RecordAndSubmitAsync(Learner, "PART_5");
        await ConsentAsync(Learner, attemptId);
        _speech.Failure = new SpeakingEvaluatorException("SPEECH_AUTHENTICATION", "Chưa cấu hình đúng.", false);

        await ProcessAsync();
        Assert.Equal("FAILED", (await GetAsync(Learner, attemptId)).Evaluation!.Status);
        _clock.Advance(3600);
        Assert.False(await ProcessAsync());

        _speech.Failure = null;
        Assert.Equal("QUEUED", (await ConsentAsync(Learner, attemptId)).Evaluation!.Status);
        await ProcessAsync();
        var completed = (await GetAsync(Learner, attemptId)).Evaluation!;
        Assert.Equal(("COMPLETED", 0, (string?)null), (completed.Status, completed.RetryCount, completed.FailureMessage));
    }

    [Fact]
    public async Task An_incomplete_language_model_answer_is_rejected_and_retried()
    {
        _language.Override = work => new SpeakingLanguageResult("Tóm tắt.", [], [],
            [new SpeakingLanguageItemResult(work.Items[0].Position, 70, 70, 70, "Nhận xét.", "")]);
        var attemptId = await RecordAndSubmitAsync(Learner, "PART_3");
        await ConsentAsync(Learner, attemptId);

        await ProcessAsync();

        var evaluation = _evaluations.Evaluations.Single();
        Assert.Equal((SpeakingEvaluationStatus.Retrying, "INVALID_RESPONSE"), (evaluation.Status, evaluation.FailureCode));
    }

    [Fact]
    public async Task Recordings_without_speech_score_zero_without_calling_the_language_model()
    {
        _speech.Result = _ => new SpeakingSpeechResult(SpeakingRecognitionStatus.NoSpeech, "", "", 30, 0, 0, null, 0, []);
        var attemptId = await RecordAndSubmitAsync(Learner, "PART_2");
        await ConsentAsync(Learner, attemptId);

        await ProcessAsync();

        var evaluation = (await GetAsync(Learner, attemptId)).Evaluation!;
        Assert.Equal(("COMPLETED", 0), (evaluation.Status, evaluation.EstimatedScore));
        Assert.All(evaluation.Items, item => Assert.Equal(("NO_SPEECH", 0), (item.RecognitionStatus, item.RawScore)));
        Assert.Empty(_language.Calls);
        Assert.NotEmpty(evaluation.Improvements);
    }

    private Task<bool> ProcessAsync() =>
        new SpeakingEvaluationProcessor(_evaluations, _store, _speech, _language, _clock)
            .ProcessNextAsync(CancellationToken.None);

    private async Task<SpeakingAttemptDto> ConsentAsync(SpeakingAttemptOwner owner, Guid attemptId)
    {
        var command = new GrantSpeakingEvaluationConsentCommand(owner, attemptId,
            new GrantSpeakingEvaluationConsentInput(true));
        await new GrantSpeakingEvaluationConsentCommandValidator().ValidateAndThrowAsync(command);
        return await new GrantSpeakingEvaluationConsentCommandHandler(_store, _evaluations, _settings, _clock)
            .Handle(command, CancellationToken.None);
    }

    private Task<SpeakingAttemptDto> GetAsync(SpeakingAttemptOwner owner, Guid attemptId) =>
        new GetSpeakingAttemptQueryHandler(_store, _evaluations, _clock).Handle(
            new GetSpeakingAttemptQuery(owner, attemptId), CancellationToken.None);

    private async Task<Guid> StartAsync(SpeakingAttemptOwner owner, string mode)
    {
        _testId ??= (await SpeakingAdministrationTests.CreatePublishedTestAsync(_store, _clock)).Id;
        return (await new StartSpeakingAttemptCommandHandler(_store, _clock).Handle(
            new StartSpeakingAttemptCommand(owner, new StartSpeakingAttemptInput(_testId.Value, mode)),
            CancellationToken.None)).Id;
    }

    /// <summary>Walks every phase, uploading a recording (ending in its position) while each listed question responds.</summary>
    private async Task<Guid> RecordAndSubmitAsync(SpeakingAttemptOwner owner, string mode, int[]? recorded = null,
        bool closeUploadWindow = true)
    {
        var attemptId = await StartAsync(owner, mode);
        var next = new NextSpeakingPhaseCommandHandler(_store, _clock);
        var upload = new UploadSpeakingRecordingCommandHandler(_store, _store, _clock);
        var attempt = await new GetSpeakingAttemptQueryHandler(_store, _evaluations, _clock).Handle(
            new GetSpeakingAttemptQuery(owner, attemptId), CancellationToken.None);
        while (attempt.Status == "IN_PROGRESS")
        {
            var position = attempt.CurrentQuestionPosition!.Value;
            if (attempt.CurrentPhase == "RESPONDING" && (recorded is null || recorded.Contains(position)))
                await upload.Handle(new UploadSpeakingRecordingCommand(owner, attemptId, position,
                    [.. SpeakingSamples.WebmAudio, (byte)position], "audio/webm", 10), CancellationToken.None);
            attempt = await next.Handle(new NextSpeakingPhaseCommand(owner, attemptId), CancellationToken.None);
        }
        if (closeUploadWindow)
            _clock.Advance(16);
        return attemptId;
    }
}

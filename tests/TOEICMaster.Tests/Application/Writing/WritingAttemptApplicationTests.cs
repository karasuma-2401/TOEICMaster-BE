using System.Text.Json;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Application.Writing.Attempts;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.TestDoubles;

namespace TOEICMaster.Tests.Application.Writing;

public sealed class WritingAttemptApplicationTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 10, 23, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Attempt_owner_validation_accepts_exactly_one_valid_identity()
    {
        var validator = new WritingAttemptOwnerValidator();
        Assert.True(validator.Validate(WritingAttemptOwner.ForUser(Guid.NewGuid())).IsValid);
        Assert.True(validator.Validate(WritingAttemptOwner.ForAnonymousSession(Guid.NewGuid().ToString())).IsValid);
        Assert.False(validator.Validate(new WritingAttemptOwner(null, null)).IsValid);
        Assert.False(validator.Validate(new WritingAttemptOwner(Guid.NewGuid(), "guest-hash")).IsValid);
        Assert.False(validator.Validate(new WritingAttemptOwner(Guid.Empty, null)).IsValid);
        Assert.False(validator.Validate(new WritingAttemptOwner(Guid.Empty, "guest-hash")).IsValid);
    }

    [Fact]
    public async Task Existing_test_attempt_keeps_its_answer_and_image_after_test_is_archived()
    {
        var owner = Guid.NewGuid();
        var asset = WritingMediaAsset.Create("asset-key", "image.png", "image/png", 3, owner, StartedAt);
        var exercise = WritingExercise.Create(WritingPart.Part1, "WP1-01", "Work", WritingDifficulty.Easy,
            WritingExerciseStatus.Published, "", "", asset.Id, "A photo", "walk", "while", null, null, null,
            null, null, null, null, null, owner, StartedAt);
        var test = WritingTest.Create("WRT-1", "Test", "", owner, StartedAt);
        test.Publish(StartedAt, owner);
        var attemptId = Guid.NewGuid();
        var question = WritingAttemptQuestion.Create(attemptId, Guid.NewGuid(), 1, WritingPart.Part1, 30, 60,
            WritingAttemptMapping.ToPromptSnapshotJson(JsonSerializer.Serialize(exercise.ToSnapshot())), "{}");
        var attempt = WritingAttempt.Create(attemptId, test.Id, test.Code, test.Title, WritingAttemptMode.Full,
            owner, null, [question], StartedAt);
        test.Archive(owner, StartedAt.AddSeconds(31));
        attempt.AdvanceExpired(StartedAt.AddSeconds(30));
        attempt.SaveAnswer(1, "A person walks while working.", StartedAt.AddSeconds(31));
        var handler = new GetWritingAttemptImageQueryHandler(new EmptyWritingAttemptRepository { Attempt = attempt },
            new EmptyWritingAdminRepository { MediaAsset = asset }, new EmptyWritingImageStore { Data = [1, 2, 3] });

        var image = await handler.Handle(new GetWritingAttemptImageQuery(WritingAttemptOwner.ForUser(owner), attemptId,
            asset.Id), CancellationToken.None);
        Assert.Equal(WritingTestStatus.Archived, test.Status);
        Assert.Equal("A person walks while working.", Assert.Single(attempt.Questions).Answer);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.Data);
    }

    [Fact]
    public async Task Attempt_image_requires_the_owner_and_an_image_in_its_snapshot()
    {
        var owner = Guid.NewGuid();
        var asset = WritingMediaAsset.Create("asset-key", "image.png", "image/png", 3, owner, StartedAt);
        var exercise = WritingExercise.Create(WritingPart.Part1, "WP1-01", "Work", WritingDifficulty.Easy,
            WritingExerciseStatus.Published, "", "", asset.Id, "A photo", "walk", "while", null, null, null,
            null, null, null, null, null, owner, StartedAt);
        var attemptId = Guid.NewGuid();
        var snapshot = JsonSerializer.Serialize(exercise.ToSnapshot());
        var question = WritingAttemptQuestion.CreatePractice(attemptId, WritingPart.Part1,
            WritingAttemptMapping.ToPromptSnapshotJson(snapshot), "{}");
        var attempt = WritingAttempt.CreatePractice(attemptId, exercise, owner, null, question, StartedAt);
        var handler = new GetWritingAttemptImageQueryHandler(new EmptyWritingAttemptRepository { Attempt = attempt },
            new EmptyWritingAdminRepository { MediaAsset = asset }, new EmptyWritingImageStore { Data = [1, 2, 3] });

        var image = await handler.Handle(new GetWritingAttemptImageQuery(WritingAttemptOwner.ForUser(owner), attemptId,
            asset.Id), CancellationToken.None);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.Data);
        await Assert.ThrowsAsync<WritingAttemptNotFoundException>(() => handler.Handle(
            new GetWritingAttemptImageQuery(WritingAttemptOwner.ForUser(Guid.NewGuid()), attemptId, asset.Id), CancellationToken.None));
        await Assert.ThrowsAsync<WritingAttemptNotFoundException>(() => handler.Handle(
            new GetWritingAttemptImageQuery(WritingAttemptOwner.ForUser(owner), attemptId, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public void Practice_attempt_allows_writing_without_a_deadline_and_keeps_the_owner()
    {
        var exercise = WritingExercise.Create(WritingPart.Part3, "WP3-001", "Work", WritingDifficulty.Easy,
            WritingExerciseStatus.Published, "", "", null, null, null, null, null, null, null, null, null,
            "Explain your opinion.", 300, null, Guid.NewGuid(), StartedAt);
        var attemptId = Guid.NewGuid();
        var question = WritingAttemptQuestion.CreatePractice(attemptId, WritingPart.Part3, "{}", "{}");
        var ownerId = Guid.NewGuid();
        var attempt = WritingAttempt.CreatePractice(attemptId, exercise, ownerId, null, question, StartedAt);

        Assert.True(attempt.IsOwnedBy(ownerId, null));
        Assert.False(attempt.IsOwnedBy(Guid.NewGuid(), null));
        Assert.False(attempt.AdvanceExpired(StartedAt.AddDays(1)));
        attempt.SaveAnswer(1, "My opinion", StartedAt.AddDays(1));
        Assert.Equal("My opinion", Assert.Single(attempt.Questions).Answer);
        Assert.Null(attempt.ToDto(StartedAt).PhaseEndsAtUtc);
        Assert.True(attempt.ToDto(StartedAt).IsPractice);
    }

    [Fact]
    public void Server_time_advances_preparation_then_locks_and_submits_without_client_input()
    {
        var attempt = CreatePartOneAttempt();

        Assert.True(attempt.AdvanceExpired(StartedAt.AddSeconds(30)));
        Assert.Equal(WritingAttemptPhase.Answering, attempt.CurrentPhase);
        Assert.Equal(StartedAt.AddSeconds(90), attempt.PhaseEndsAtUtc);

        Assert.True(attempt.AdvanceExpired(StartedAt.AddSeconds(90)));
        Assert.Equal(WritingAttemptStatus.SubmittedAwaitingEvaluation, attempt.Status);
        Assert.Equal(StartedAt.AddSeconds(90), attempt.SubmittedAtUtc);
        Assert.NotNull(Assert.Single(attempt.Questions).LockedAtUtc);
    }

    [Fact]
    public void Unordered_loaded_questions_save_and_lock_by_position()
    {
        var attempt = CreateMultiQuestionAttempt();
        var loadedQuestions = Assert.IsType<List<WritingAttemptQuestion>>(attempt.Questions);
        loadedQuestions.Reverse();

        attempt.AdvanceExpired(StartedAt.AddSeconds(10));
        Assert.Equal(StartedAt.AddSeconds(30), attempt.PhaseEndsAtUtc);
        attempt.SaveAnswer(1, "First answer", StartedAt.AddSeconds(11));
        attempt.Next(StartedAt.AddSeconds(12));

        var first = Assert.Single(attempt.Questions, question => question.Position == 1);
        var second = Assert.Single(attempt.Questions, question => question.Position == 2);
        var third = Assert.Single(attempt.Questions, question => question.Position == 3);
        Assert.Equal("First answer", first.Answer);
        Assert.NotNull(first.LockedAtUtc);
        Assert.Null(second.LockedAtUtc);
        Assert.Null(third.LockedAtUtc);
        Assert.Equal(StartedAt.AddSeconds(42), attempt.PhaseEndsAtUtc);

        attempt.AdvanceExpired(StartedAt.AddSeconds(42));
        Assert.Equal(StartedAt.AddSeconds(82), attempt.PhaseEndsAtUtc);
        attempt.SaveAnswer(2, "Second answer", StartedAt.AddSeconds(43));
        attempt.Submit(StartedAt.AddSeconds(44));
        Assert.Equal("Second answer", second.Answer);
        Assert.NotNull(second.LockedAtUtc);
        Assert.Null(third.LockedAtUtc);
    }

    [Fact]
    public void Expired_unordered_questions_advance_with_the_correct_timing()
    {
        var attempt = CreateMultiQuestionAttempt();
        Assert.IsType<List<WritingAttemptQuestion>>(attempt.Questions).Reverse();

        attempt.AdvanceExpired(StartedAt.AddSeconds(100));

        Assert.Equal(2, attempt.CurrentQuestionIndex);
        Assert.Equal(WritingAttemptPhase.Preparing, attempt.CurrentPhase);
        Assert.Equal(StartedAt.AddSeconds(150), attempt.PhaseEndsAtUtc);
        Assert.All(attempt.Questions.Where(question => question.Position < 3),
            question => Assert.NotNull(question.LockedAtUtc));
        Assert.Null(Assert.Single(attempt.Questions, question => question.Position == 3).LockedAtUtc);
    }

    [Fact]
    public void Attempt_prompt_snapshot_excludes_admin_model_answer_and_rubric()
    {
        var promptSnapshot = WritingAttemptMapping.ToPromptSnapshotJson(FrozenAdminSnapshot());

        Assert.DoesNotContain("Private rubric", promptSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("Private model answer", promptSnapshot, StringComparison.Ordinal);
        var prompt = JsonSerializer.Deserialize<WritingAttemptPromptSnapshot>(promptSnapshot);
        Assert.Equal("walk", prompt?.KeywordOne);
        Assert.Equal("while", prompt?.KeywordTwo);
    }

    [Fact]
    public void Model_answer_snapshot_is_not_returned_even_after_evaluation_completes()
    {
        var attempt = CreatePartOneAttempt(includeReviewSnapshot: true);

        Assert.Empty(attempt.ToDto(StartedAt).ReviewQuestions);

        attempt.AdvanceExpired(StartedAt.AddSeconds(30));
        attempt.AdvanceExpired(StartedAt.AddSeconds(90));
        var evaluation = WritingEvaluation.Create(attempt.Id, "gemini", "gemini-2.5-flash-lite", "writing-v1",
            StartedAt);
        evaluation.GrantConsentAndQueue(StartedAt, "gemini", "gemini-2.5-flash-lite", "writing-v1");
        evaluation.BeginProcessing(StartedAt, StartedAt.AddMinutes(2));
        evaluation.Complete(200, "Nhận xét", [], [], [WritingEvaluationFeedback.Create(evaluation.Id, 1, 3,
            "Nhận xét theo câu.", "Gợi ý ngắn.")], StartedAt.AddMinutes(1));

        var result = attempt.ToDto(StartedAt.AddMinutes(1), evaluation);

        Assert.Empty(result.ReviewQuestions);
    }

    [Fact]
    public void Saved_answer_is_rejected_after_the_server_has_locked_the_question()
    {
        var attempt = CreatePartOneAttempt();
        attempt.AdvanceExpired(StartedAt.AddSeconds(30));
        attempt.AdvanceExpired(StartedAt.AddSeconds(90));

        Assert.Throws<InvalidOperationException>(() => attempt.SaveAnswer(1, "Late answer", StartedAt.AddSeconds(91)));
    }

    [Fact]
    public void Evaluation_requires_explicit_consent_then_keeps_a_valid_estimated_score()
    {
        var evaluation = WritingEvaluation.Create(Guid.NewGuid(), "gemini", "gemini-2.5-flash-lite", "writing-v1",
            StartedAt);

        Assert.Equal(WritingEvaluationStatus.ConsentRequired, evaluation.Status);
        Assert.False(evaluation.CanBeClaimed(StartedAt));
        Assert.True(evaluation.GrantConsentAndQueue(StartedAt, "gemini", "gemini-2.5-flash-lite", "writing-v1"));
        evaluation.BeginProcessing(StartedAt, StartedAt.AddMinutes(2));
        var feedback = WritingEvaluationFeedback.Create(evaluation.Id, 1, 3, "Câu có đủ từ gợi ý.",
            "Thêm mạo từ để câu tự nhiên hơn.");

        evaluation.Complete(160, "Bài có ý rõ ràng và cần cải thiện độ chính xác ngữ pháp.", ["Đáp ứng đúng yêu cầu."],
            ["Rà soát mạo từ."], [feedback], StartedAt.AddSeconds(3));

        Assert.Equal(WritingEvaluationStatus.Completed, evaluation.Status);
        Assert.Equal(160, evaluation.EstimatedScore);
        Assert.Single(evaluation.Feedback);
    }

    [Fact]
    public void Evaluation_rejects_scores_outside_the_toeic_writing_scale()
    {
        var evaluation = WritingEvaluation.Create(Guid.NewGuid(), "gemini", "gemini-2.5-flash-lite", "writing-v1",
            StartedAt);
        evaluation.GrantConsentAndQueue(StartedAt, "gemini", "gemini-2.5-flash-lite", "writing-v1");
        evaluation.BeginProcessing(StartedAt, StartedAt.AddMinutes(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => evaluation.Complete(155, "Nhận xét", [], [], [], StartedAt));
    }

    [Fact]
    public void User_retry_uses_current_model_and_preserves_original_consent()
    {
        var evaluation = WritingEvaluation.Create(Guid.NewGuid(), "gemini", "old-model", "writing-v1", StartedAt);
        Assert.True(evaluation.GrantConsentAndQueue(StartedAt, "gemini", "old-model", "writing-v1"));
        evaluation.BeginProcessing(StartedAt, StartedAt.AddMinutes(2));
        evaluation.Fail("PROVIDER_REJECTED", "Unavailable", StartedAt.AddSeconds(1));

        Assert.True(evaluation.GrantConsentAndQueue(StartedAt.AddMinutes(1), "gemini", "new-model", "writing-v2"));
        Assert.Equal(WritingEvaluationStatus.Queued, evaluation.Status);
        Assert.Equal("new-model", evaluation.Model);
        Assert.Equal("writing-v2", evaluation.RubricVersion);
        Assert.Equal(StartedAt, evaluation.ConsentAcceptedAtUtc);
        Assert.Equal(0, evaluation.RetryCount);
        Assert.Null(evaluation.FailureCode);
        Assert.False(evaluation.GrantConsentAndQueue(StartedAt.AddMinutes(2), "gemini", "other-model", "writing-v3"));
        Assert.Equal("new-model", evaluation.Model);
    }

    private static WritingAttempt CreateMultiQuestionAttempt()
    {
        var attemptId = Guid.NewGuid();
        var questions = new[]
        {
            WritingAttemptQuestion.Create(attemptId, Guid.NewGuid(), 1, WritingPart.Part1, 10, 20, "{}"),
            WritingAttemptQuestion.Create(attemptId, Guid.NewGuid(), 2, WritingPart.Part2, 30, 40, "{}"),
            WritingAttemptQuestion.Create(attemptId, Guid.NewGuid(), 3, WritingPart.Part3, 50, 60, "{}")
        };
        return WritingAttempt.Create(attemptId, Guid.NewGuid(), "WRT-01", "Writing test", WritingAttemptMode.Full,
            Guid.NewGuid(), null, questions, StartedAt);
    }

    private static WritingAttempt CreatePartOneAttempt(bool includeReviewSnapshot = false)
    {
        var attemptId = Guid.NewGuid();
        var snapshot = FrozenAdminSnapshot();
        var question = WritingAttemptQuestion.Create(attemptId, Guid.NewGuid(), 1, WritingPart.Part1, 30, 60,
            WritingAttemptMapping.ToPromptSnapshotJson(snapshot), includeReviewSnapshot
                ? WritingAttemptMapping.ToReviewSnapshotJson(snapshot)
                : "{}");
        return WritingAttempt.Create(attemptId, Guid.NewGuid(), "WRT-01", "Writing test", WritingAttemptMode.Part1,
            Guid.NewGuid(), null, [question], StartedAt);
    }

    private static string FrozenAdminSnapshot() => JsonSerializer.Serialize(new WritingExerciseSnapshot(Guid.NewGuid(),
        1, "WP1-01", "Work", "EASY", "Private rubric", "Private model answer", Guid.NewGuid(), "A photo",
        "walk", "while", null, null, null, null, null, null, null, null));
}

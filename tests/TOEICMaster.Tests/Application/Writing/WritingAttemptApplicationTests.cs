using System.Text.Json;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Application.Writing.Attempts;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Writing;

public sealed class WritingAttemptApplicationTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 10, 23, 7, 0, 0, TimeSpan.Zero);

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
    public void Model_answer_snapshot_is_only_returned_after_evaluation_completes()
    {
        var attempt = CreatePartOneAttempt(includeReviewSnapshot: true);

        Assert.Empty(attempt.ToDto(StartedAt).ReviewQuestions);

        attempt.AdvanceExpired(StartedAt.AddSeconds(30));
        attempt.AdvanceExpired(StartedAt.AddSeconds(90));
        var evaluation = WritingEvaluation.Create(attempt.Id, "gemini", "gemini-2.5-flash-lite", "writing-v1",
            StartedAt);
        evaluation.GrantConsentAndQueue(StartedAt);
        evaluation.BeginProcessing(StartedAt, StartedAt.AddMinutes(2));
        evaluation.Complete(200, "Nhận xét", [], [], [WritingEvaluationFeedback.Create(evaluation.Id, 1, 3,
            "Nhận xét theo câu.", "Gợi ý ngắn.")], StartedAt.AddMinutes(1));

        var result = attempt.ToDto(StartedAt.AddMinutes(1), evaluation);

        Assert.Equal("Private model answer", Assert.Single(result.ReviewQuestions).ModelAnswer);
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
        Assert.True(evaluation.GrantConsentAndQueue(StartedAt));
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
        evaluation.GrantConsentAndQueue(StartedAt);
        evaluation.BeginProcessing(StartedAt, StartedAt.AddMinutes(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => evaluation.Complete(155, "Nhận xét", [], [], [], StartedAt));
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

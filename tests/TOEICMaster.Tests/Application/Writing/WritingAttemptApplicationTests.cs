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
    public void Saved_answer_is_rejected_after_the_server_has_locked_the_question()
    {
        var attempt = CreatePartOneAttempt();
        attempt.AdvanceExpired(StartedAt.AddSeconds(30));
        attempt.AdvanceExpired(StartedAt.AddSeconds(90));

        Assert.Throws<InvalidOperationException>(() => attempt.SaveAnswer(1, "Late answer", StartedAt.AddSeconds(91)));
    }

    private static WritingAttempt CreatePartOneAttempt()
    {
        var attemptId = Guid.NewGuid();
        var question = WritingAttemptQuestion.Create(attemptId, Guid.NewGuid(), 1, WritingPart.Part1, 30, 60,
            WritingAttemptMapping.ToPromptSnapshotJson(FrozenAdminSnapshot()));
        return WritingAttempt.Create(attemptId, Guid.NewGuid(), "WRT-01", "Writing test", WritingAttemptMode.Part1,
            Guid.NewGuid(), null, [question], StartedAt);
    }

    private static string FrozenAdminSnapshot() => JsonSerializer.Serialize(new WritingExerciseSnapshot(Guid.NewGuid(),
        1, "WP1-01", "Work", "EASY", "Private rubric", "Private model answer", Guid.NewGuid(), "A photo",
        "walk", "while", null, null, null, null, null, null, null, null));
}

using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Domain;

public sealed class SpeakingAttemptStateTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 2, 180)]
    [InlineData(2, 2, 150)]
    [InlineData(3, 1, 69)]
    [InlineData(4, 1, 114)]
    [InlineData(5, 1, 105)]
    public void Part_durations_match_the_fixed_toeic_format(int part, int exercises, int seconds)
    {
        Assert.Equal(exercises, SpeakingTiming.SlotsPerPart(part));
        Assert.Equal(seconds, SpeakingTiming.PartDurationSeconds(part, exercises));
    }

    [Fact]
    public void Full_test_has_eleven_questions_with_standard_timing()
    {
        var attempt = FullAttempt();

        Assert.Equal(11, attempt.Questions.Count);
        Assert.Equal([45, 45, 45, 45, 3, 3, 3, 3, 3, 3, 45], attempt.Questions.Select(q => q.PreparationSeconds));
        Assert.Equal([45, 45, 30, 30, 15, 15, 30, 15, 15, 30, 60], attempt.Questions.Select(q => q.ResponseSeconds));
        Assert.Equal([0, 0, 0, 0, 0, 0, 0, 45, 0, 0, 0], attempt.Questions.Select(q => q.ReadingSeconds));
    }

    [Fact]
    public void A_question_without_reading_starts_with_preparation_then_response_then_locks()
    {
        var attempt = FullAttempt();
        Assert.Equal((1, SpeakingPhase.Preparing, Start.AddSeconds(45)),
            (attempt.CurrentPosition, attempt.CurrentPhase, attempt.PhaseEndsAtUtc));

        Assert.True(attempt.AdvanceExpired(Start.AddSeconds(45)));
        Assert.Equal((1, SpeakingPhase.Responding, Start.AddSeconds(90)),
            (attempt.CurrentPosition, attempt.CurrentPhase, attempt.PhaseEndsAtUtc));

        attempt.AdvanceExpired(Start.AddSeconds(90));
        Assert.Equal(Start.AddSeconds(90), attempt.Questions[0].LockedAtUtc);
        Assert.Equal((2, SpeakingPhase.Preparing), (attempt.CurrentPosition, attempt.CurrentPhase));
    }

    [Fact]
    public void Advancing_after_a_long_absence_walks_every_deadline_and_submits()
    {
        var attempt = FullAttempt();
        var totalSeconds = Enumerable.Range(1, 5).Sum(part =>
            SpeakingTiming.PartDurationSeconds(part, SpeakingTiming.SlotsPerPart(part)));

        attempt.AdvanceExpired(Start.AddHours(3));

        Assert.Equal(SpeakingAttemptStatus.Submitted, attempt.Status);
        Assert.Equal(Start.AddSeconds(totalSeconds), attempt.SubmittedAtUtc);
        Assert.All(attempt.Questions, question => Assert.NotNull(question.LockedAtUtc));
        Assert.Null(attempt.CurrentPosition);
        Assert.Null(attempt.CurrentPhase);
        Assert.Null(attempt.PhaseEndsAtUtc);
    }

    [Fact]
    public void Part_four_reads_only_before_its_first_question()
    {
        var attempt = Attempt(SpeakingAttemptMode.Part4, Questions(4));
        Assert.Equal((SpeakingPhase.Reading, Start.AddSeconds(45)), (attempt.CurrentPhase, attempt.PhaseEndsAtUtc));

        attempt.Next(Start.AddSeconds(10));
        Assert.Equal((SpeakingPhase.Preparing, Start.AddSeconds(13)), (attempt.CurrentPhase, attempt.PhaseEndsAtUtc));
        attempt.AdvanceExpired(Start.AddSeconds(13 + 15));

        Assert.Equal((2, SpeakingPhase.Preparing), (attempt.CurrentPosition, attempt.CurrentPhase));
    }

    [Fact]
    public void Next_during_the_last_response_submits_and_later_calls_change_nothing()
    {
        var attempt = Attempt(SpeakingAttemptMode.Part5, Questions(5));
        attempt.Next(Start.AddSeconds(5));
        Assert.Equal(SpeakingPhase.Responding, attempt.CurrentPhase);

        attempt.Next(Start.AddSeconds(20));
        Assert.Equal(SpeakingAttemptStatus.Submitted, attempt.Status);
        Assert.Equal(Start.AddSeconds(20), attempt.Questions[0].LockedAtUtc);

        var version = attempt.Version;
        attempt.Next(Start.AddSeconds(30));
        attempt.Submit(Start.AddSeconds(40));
        Assert.Equal(version, attempt.Version);
        Assert.Equal(Start.AddSeconds(20), attempt.SubmittedAtUtc);
    }

    [Fact]
    public void Recordings_are_accepted_while_responding_and_for_fifteen_seconds_after_locking()
    {
        var attempt = Attempt(SpeakingAttemptMode.Part1, Questions(1).Concat(Questions(1, startPosition: 2)).ToArray());
        Assert.False(attempt.CanAcceptRecording(1, Start), "preparation is not a response window");

        attempt.Next(Start.AddSeconds(1));
        Assert.True(attempt.CanAcceptRecording(1, Start.AddSeconds(2)));
        Assert.False(attempt.CanAcceptRecording(2, Start.AddSeconds(2)), "a future question cannot be recorded");

        attempt.Next(Start.AddSeconds(10));
        Assert.True(attempt.CanAcceptRecording(1, Start.AddSeconds(25)));
        Assert.False(attempt.CanAcceptRecording(1, Start.AddSeconds(26)));
        Assert.Throws<InvalidOperationException>(() =>
            attempt.AttachRecording(1, Guid.NewGuid(), 9, Start.AddSeconds(26)));
    }

    [Fact]
    public void Attaching_a_new_recording_returns_the_replaced_asset()
    {
        var attempt = Attempt(SpeakingAttemptMode.Part5, Questions(5));
        attempt.Next(Start);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        Assert.Null(attempt.AttachRecording(1, first, 10, Start.AddSeconds(10)));
        Assert.Equal(first, attempt.AttachRecording(1, second, 12, Start.AddSeconds(12)));
        Assert.Equal((second, 12), (attempt.Questions[0].RecordingAssetId, attempt.Questions[0].RecordingDurationSeconds));
    }

    [Fact]
    public void Questions_are_found_by_position_even_when_loaded_out_of_order()
    {
        var attempt = Attempt(SpeakingAttemptMode.Part3, Questions(3));
        // EF Core may materialise the collection in any order; simulate the worst case.
        var field = typeof(SpeakingAttempt).GetField("_questions",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        ((List<SpeakingAttemptQuestion>)field.GetValue(attempt)!).Reverse();

        attempt.Next(Start);
        var recording = Guid.NewGuid();
        attempt.AttachRecording(1, recording, 5, Start.AddSeconds(1));
        attempt.Next(Start.AddSeconds(1));
        attempt.Next(Start.AddSeconds(1));

        SpeakingAttemptQuestion At(int position) => attempt.Questions.Single(question => question.Position == position);
        Assert.Equal([1, 2, 3], attempt.Questions.Select(question => question.Position));
        Assert.Equal(recording, At(1).RecordingAssetId);
        Assert.Equal((Start.AddSeconds(1), Start.AddSeconds(16)), (At(1).LockedAtUtc, attempt.PhaseEndsAtUtc));
        Assert.Null(At(3).RecordingAssetId);
        Assert.Same(At(2), attempt.FindQuestion(2));
        Assert.Equal((2, SpeakingPhase.Responding), (attempt.CurrentPosition, attempt.CurrentPhase));
    }

    [Fact]
    public void An_attempt_needs_exactly_one_owner()
    {
        Assert.Throws<InvalidOperationException>(() => SpeakingAttempt.Create(Guid.NewGuid(), Guid.NewGuid(),
            SpeakingAttemptMode.Part5, null, null, Questions(5), Start));
        Assert.Throws<InvalidOperationException>(() => SpeakingAttempt.Create(Guid.NewGuid(), Guid.NewGuid(),
            SpeakingAttemptMode.Part5, Guid.NewGuid(), new string('A', 64), Questions(5), Start));
    }

    private static SpeakingAttempt FullAttempt()
    {
        var questions = new List<SpeakingAttemptQuestion>();
        foreach (var part in Enumerable.Range(1, 5))
            for (var slot = 0; slot < SpeakingTiming.SlotsPerPart(part); slot++)
                questions.AddRange(Questions(part, questions.Count + 1));
        return Attempt(SpeakingAttemptMode.Full, questions);
    }

    private static SpeakingAttemptQuestion[] Questions(int part, int startPosition = 1) =>
        Enumerable.Range(0, SpeakingTiming.QuestionsPerExercise(part)).Select(index =>
            SpeakingAttemptQuestion.Create(Guid.Empty, startPosition + index, part, index, Guid.NewGuid(), "{}", "{}"))
            .ToArray();

    private static SpeakingAttempt Attempt(SpeakingAttemptMode mode, IReadOnlyList<SpeakingAttemptQuestion> questions) =>
        SpeakingAttempt.Create(Guid.NewGuid(), Guid.NewGuid(), mode, Guid.NewGuid(), null, questions, Start);
}

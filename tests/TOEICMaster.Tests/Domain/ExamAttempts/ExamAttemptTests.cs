using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Domain.ExamAttempts;

public sealed class ExamAttemptTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

    private static ExamAttempt NewAttempt() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), StartedAt);

    private static ScoreProfile CompleteProfile()
    {
        var profile = ScoreProfile.Create("Test profile", StartedAt);
        foreach (var skill in new[] { ExamSkill.Listening, ExamSkill.Reading })
            for (var raw = 0; raw <= 100; raw++)
                profile.SetScore(skill, raw, Math.Min(495, 5 + (raw * 5)));
        return profile;
    }

    [Fact]
    public void Time_limit_defaults_to_two_hours_and_the_deadline_follows_the_start()
    {
        var attempt = NewAttempt();

        Assert.Equal(7200, attempt.TimeLimitSeconds);
        Assert.Equal(StartedAt.AddHours(2), attempt.DeadlineUtc);
        Assert.Equal(3600, attempt.GetRemainingSeconds(StartedAt.AddHours(1)));
        Assert.False(attempt.IsTimeUp(StartedAt.AddHours(2).AddSeconds(-1)));
        Assert.True(attempt.IsTimeUp(StartedAt.AddHours(2)));
        Assert.False(attempt.IsTimeUp(StartedAt.AddHours(2), graceSeconds: 5));
    }

    [Fact]
    public void Starting_reading_locks_listening_answers_but_not_reading_answers()
    {
        var attempt = NewAttempt();
        attempt.UpsertAnswer(Guid.NewGuid(), 1, 1, "A", null, StartedAt);

        attempt.StartReading(StartedAt.AddMinutes(45));

        Assert.True(attempt.IsListeningLocked);
        Assert.Throws<InvalidOperationException>(() =>
            attempt.UpsertAnswer(Guid.NewGuid(), 32, 3, "B", null, StartedAt.AddMinutes(46)));
        Assert.Throws<InvalidOperationException>(() =>
            attempt.ToggleFlag(Guid.NewGuid(), 71, 4, true, StartedAt.AddMinutes(46)));

        attempt.UpsertAnswer(Guid.NewGuid(), 101, 5, "C", null, StartedAt.AddMinutes(46));
        Assert.Equal(2, attempt.Answers.Count);
    }

    [Fact]
    public void Starting_reading_twice_keeps_the_first_timestamp()
    {
        var attempt = NewAttempt();

        attempt.StartReading(StartedAt.AddMinutes(45));
        attempt.StartReading(StartedAt.AddMinutes(50));

        Assert.Equal(StartedAt.AddMinutes(45), attempt.ReadingStartedAtUtc);
    }

    [Fact]
    public void Grading_counts_listening_and_reading_separately_and_converts_through_the_profile()
    {
        var attempt = NewAttempt();
        var listening = Guid.NewGuid();
        var reading = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var skipped = Guid.NewGuid();
        attempt.UpsertAnswer(listening, 1, 1, "A", null, StartedAt);
        attempt.UpsertAnswer(reading, 101, 5, "C", null, StartedAt);
        attempt.UpsertAnswer(wrong, 102, 5, "A", null, StartedAt);
        attempt.RecordBlankAnswer(skipped, 103, 5, StartedAt);

        attempt.GradeAndComplete(
            CompleteProfile(),
            new Dictionary<Guid, string?>
            {
                [listening] = "A",
                [reading] = "c",
                [wrong] = "B",
                [skipped] = "D"
            },
            StartedAt.AddMinutes(100));

        Assert.Equal(ExamAttemptStatus.Completed, attempt.Status);
        Assert.Equal(1, attempt.ListeningRawScore);
        Assert.Equal(1, attempt.ReadingRawScore);
        Assert.Equal(10, attempt.ListeningScaledScore);
        Assert.Equal(10, attempt.ReadingScaledScore);
        Assert.Equal(20, attempt.TotalScaledScore);
        Assert.False(attempt.IsTimeExpired);
        Assert.Equal(true, attempt.Answers.Single(a => a.QuestionId == reading).IsCorrect);
        Assert.Equal(false, attempt.Answers.Single(a => a.QuestionId == wrong).IsCorrect);
        Assert.Equal(false, attempt.Answers.Single(a => a.QuestionId == skipped).IsCorrect);
    }

    [Fact]
    public void Timed_out_grading_flags_the_attempt_as_expired()
    {
        var attempt = NewAttempt();

        attempt.GradeAndComplete(
            CompleteProfile(),
            new Dictionary<Guid, string?>(),
            attempt.DeadlineUtc,
            timedOut: true);

        Assert.True(attempt.IsTimeExpired);
        Assert.Equal(attempt.DeadlineUtc, attempt.SubmittedAtUtc);
    }

    [Fact]
    public void A_completed_attempt_cannot_be_graded_edited_or_moved_to_reading()
    {
        var attempt = NewAttempt();
        attempt.GradeAndComplete(CompleteProfile(), new Dictionary<Guid, string?>(), StartedAt.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(() =>
            attempt.GradeAndComplete(CompleteProfile(), new Dictionary<Guid, string?>(), StartedAt.AddMinutes(11)));
        Assert.Throws<InvalidOperationException>(() =>
            attempt.UpsertAnswer(Guid.NewGuid(), 101, 5, "A", null, StartedAt.AddMinutes(11)));
        Assert.Throws<InvalidOperationException>(() => attempt.StartReading(StartedAt.AddMinutes(11)));
    }
}

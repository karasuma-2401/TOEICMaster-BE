using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Domain.Exams;

public sealed class ScoreProfileTests
{
    [Fact]
    public void Score_profile_is_publish_ready_only_after_both_skills_have_all_raw_score_mappings()
    {
        var profile = ScoreProfile.Create("ETS reference pending approval", DateTimeOffset.UtcNow);

        for (var rawScore = 0; rawScore <= 100; rawScore++)
            profile.SetScore(ExamSkill.Listening, rawScore, 5);

        Assert.False(profile.IsComplete);

        for (var rawScore = 0; rawScore <= 100; rawScore++)
            profile.SetScore(ExamSkill.Reading, rawScore, 5);

        Assert.True(profile.IsComplete);
        Assert.Equal(5, profile.GetScore(ExamSkill.Listening, 0));
        Assert.Equal(5, profile.GetScore(ExamSkill.Reading, 100));
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(101, 5)]
    [InlineData(1, 4)]
    [InlineData(1, 500)]
    public void Score_profile_rejects_raw_scores_or_converted_scores_outside_the_TOEIC_scale(int rawScore,
        int convertedScore)
    {
        var profile = ScoreProfile.Create("ETS reference pending approval", DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            profile.SetScore(ExamSkill.Listening, rawScore, convertedScore));
    }
}

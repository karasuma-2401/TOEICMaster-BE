using TOEICMaster.Application.Speaking.Evaluations;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Tests.Application.Speaking;

public sealed class SpeakingEvaluationScoringTests
{
    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    [InlineData(5, 5)]
    public void Task_scales_follow_the_toeic_speaking_rating_scales(int part, int maximum) =>
        Assert.Equal(maximum, SpeakingEvaluationScoring.MaxRawScore(part));

    [Theory]
    [InlineData(1, 90, 100d, 0, 0, 0, 93)]
    [InlineData(2, 50, null, 100, 100, 100, 80)]
    [InlineData(3, 100, null, 50, 50, 0, 50)]
    [InlineData(5, 60, null, 70, 80, 90, 75)]
    public void Quality_weights_pronunciation_and_language_by_part(int part, double pronunciation,
        double? completeness, int grammar, int vocabulary, int relevance, double expected) =>
        Assert.Equal(expected, SpeakingEvaluationScoring.QualityScore(part, pronunciation, completeness, grammar,
            vocabulary, relevance), 6);

    [Theory]
    [InlineData(1, 50, null, 2)]
    [InlineData(2, 16, 90, 0)]
    [InlineData(3, 84, 90, 3)]
    [InlineData(5, 90, 90, 5)]
    [InlineData(5, 90, 39, 1)]
    [InlineData(5, 10, 39, 1)]
    [InlineData(5, 9, 10, 0)]
    public void Raw_scores_round_and_cap_off_topic_answers(int part, double quality, int? relevance, int expected) =>
        Assert.Equal(expected, SpeakingEvaluationScoring.RawScore(part, quality, relevance));

    [Fact]
    public void The_estimate_scales_raw_points_to_two_hundred_in_steps_of_ten()
    {
        var evaluationId = Guid.NewGuid();
        var items = Enumerable.Range(1, 11).Select(position =>
        {
            var part = position == 11 ? 5 : 3;
            var item = SpeakingEvaluationItem.Recognized(evaluationId, position, part, "answer", 10, 80, 80, null,
                80, 80, "[]", SpeakingEvaluationScoring.MaxRawScore(part));
            item.ApplyLanguageScores(80, 80, 80, part == 5 ? 5 : 3, "ok", null);
            return item;
        }).ToArray();

        Assert.Equal(200, SpeakingEvaluationScoring.EstimatedScore(items));
        items[0].ApplyLanguageScores(80, 80, 80, 0, "ok", null);
        Assert.Equal(180, SpeakingEvaluationScoring.EstimatedScore(items));
        Assert.Equal(0, SpeakingEvaluationScoring.EstimatedScore([]));
    }

    [Theory]
    [InlineData("Attention, shoppers! The store closes in thirty minutes.",
        "attention shoppers the store closes in thirty minutes", 100)]
    [InlineData("The store closes in thirty minutes.", "the store closes in minutes", 83.3)]
    [InlineData("The store closes soon.", "uh the the store um closes soon", 100)]
    [InlineData("Please don't run.", "please do not run", 66.7)]
    [InlineData("Read this.", "", 0)]
    public void Reading_completeness_counts_passage_words_spoken_in_order(string passage, string spoken,
        double expected) =>
        Assert.Equal(expected, SpeakingEvaluationScoring.ReadingCompleteness(passage, spoken));
}

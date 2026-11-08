using System.Text;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Application.Speaking.Evaluations;

/// <summary>
/// Converts Azure Speech and language-model criteria (0-100) into TOEIC Speaking task scores: 0-3 for questions
/// 1-10 and 0-5 for question 11, then into a 0-200 practice estimate. It is never an official ETS score.
/// </summary>
public static class SpeakingEvaluationScoring
{
    /// <summary>An answer this far off the task cannot earn more than 1 point, however fluent it is.</summary>
    public const int RelevanceGate = 40;

    public static int MaxRawScore(int part) => part == 5 ? 5 : 3;

    /// <summary>
    /// Weighted 0-100 quality of one answer. Read aloud (Part 1) is judged on pronunciation, which already blends
    /// accuracy, fluency and prosody, and on how much of the passage was read. The other parts add grammar,
    /// vocabulary and relevance, with relevance weighing more where the answer must address questions or a topic.
    /// </summary>
    public static double QualityScore(int part, double pronunciationScore, double? completenessScore,
        int grammarScore, int vocabularyScore, int relevanceScore) => part switch
        {
            1 => 0.7 * pronunciationScore + 0.3 * (completenessScore ?? 0),
            2 => 0.4 * pronunciationScore + 0.2 * grammarScore + 0.2 * vocabularyScore + 0.2 * relevanceScore,
            3 or 4 or 5 => 0.3 * pronunciationScore + 0.2 * grammarScore + 0.2 * vocabularyScore +
                           0.3 * relevanceScore,
            _ => throw new ArgumentOutOfRangeException(nameof(part))
        };

    public static int RawScore(int part, double qualityScore, int? relevanceScore)
    {
        var maximum = MaxRawScore(part);
        var raw = (int)Math.Round(Math.Clamp(qualityScore, 0, 100) / 100 * maximum, MidpointRounding.AwayFromZero);
        return relevanceScore < RelevanceGate ? Math.Min(raw, 1) : raw;
    }

    /// <summary>Sum of task scores scaled to 0-200 in steps of 10, like the official score report.</summary>
    public static int EstimatedScore(IEnumerable<SpeakingEvaluationItem> items)
    {
        var scored = items.ToArray();
        var maximum = scored.Sum(item => item.MaxRawScore);
        if (maximum == 0)
            return 0;
        var raw = scored.Sum(item => item.RawScore ?? 0);
        return (int)Math.Round(raw * 20d / maximum, MidpointRounding.AwayFromZero) * 10;
    }

    /// <summary>Percentage of passage words read in order (longest common word subsequence).</summary>
    public static double ReadingCompleteness(string passage, string spokenText)
    {
        var expected = Words(passage);
        if (expected.Length == 0)
            return 0;
        var spoken = Words(spokenText);
        var previous = new int[spoken.Length + 1];
        var current = new int[spoken.Length + 1];
        foreach (var word in expected)
        {
            for (var index = 1; index <= spoken.Length; index++)
                current[index] = word == spoken[index - 1]
                    ? previous[index - 1] + 1
                    : Math.Max(previous[index], current[index - 1]);
            (previous, current) = (current, previous);
        }
        return Math.Round(100d * previous[spoken.Length] / expected.Length, 1);
    }

    private static string[] Words(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(character) || character == '\'' ? character : ' ');
        return builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}

namespace TOEICMaster.Domain.Entities;

public readonly record struct SpeakingQuestionTiming(int ReadingSeconds, int PreparationSeconds, int ResponseSeconds)
{
    public int TotalSeconds => ReadingSeconds + PreparationSeconds + ResponseSeconds;
}

/// <summary>Fixed TOEIC Speaking structure and timing; not configurable per test.</summary>
public static class SpeakingTiming
{
    public const int MinPart = 1;
    public const int MaxPart = 5;

    public static bool IsPart(int part) => part is >= MinPart and <= MaxPart;

    /// <summary>Exercises a full test uses for the Part (2/2/1/1/1).</summary>
    public static int SlotsPerPart(int part) => part switch
    {
        1 or 2 => 2,
        3 or 4 or 5 => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(part))
    };

    /// <summary>Questions in one exercise; Part 3 and Part 4 share one context across three questions.</summary>
    public static int QuestionsPerExercise(int part) => part switch
    {
        3 or 4 => 3,
        1 or 2 or 5 => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(part))
    };

    public static SpeakingQuestionTiming For(int part, int questionIndex)
    {
        if (questionIndex < 0 || questionIndex >= QuestionsPerExercise(part))
            throw new ArgumentOutOfRangeException(nameof(questionIndex));
        return part switch
        {
            1 => new(0, 45, 45),
            2 => new(0, 45, 30),
            3 => new(0, 3, questionIndex == 2 ? 30 : 15),
            4 => new(questionIndex == 0 ? 45 : 0, 3, questionIndex == 2 ? 30 : 15),
            5 => new(0, 45, 60),
            _ => throw new ArgumentOutOfRangeException(nameof(part))
        };
    }

    public static int ExerciseDurationSeconds(int part) =>
        Enumerable.Range(0, QuestionsPerExercise(part)).Sum(index => For(part, index).TotalSeconds);

    /// <summary>Reading + preparation + response for every exercise of the Part (P1 180, P2 150, P3 69, P4 114, P5 105).</summary>
    public static int PartDurationSeconds(int part, int exerciseCount) => ExerciseDurationSeconds(part) * exerciseCount;
}

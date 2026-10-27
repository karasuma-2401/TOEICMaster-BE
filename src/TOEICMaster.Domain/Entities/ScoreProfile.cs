namespace TOEICMaster.Domain.Entities;

public enum ExamSkill
{
    Listening = 1,
    Reading = 2
}

public sealed class ScoreProfile
{
    private readonly List<ScoreConversion> _conversions = [];

    private ScoreProfile()
    {
    }

    private ScoreProfile(Guid id, string name, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<ScoreConversion> Conversions => _conversions.AsReadOnly();
    public bool IsComplete => HasAllMappings(ExamSkill.Listening) && HasAllMappings(ExamSkill.Reading);

    public static ScoreProfile Create(string name, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new ScoreProfile(Guid.NewGuid(), name.Trim(), createdAtUtc);
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void SetScore(ExamSkill skill, int rawCorrectCount, int scaledScore)
    {
        if (rawCorrectCount is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(rawCorrectCount));
        if (scaledScore is < 5 or > 495 || scaledScore % 5 != 0)
            throw new ArgumentOutOfRangeException(nameof(scaledScore));

        var existing = _conversions.SingleOrDefault(conversion => conversion.Skill == skill &&
            conversion.RawCorrectCount == rawCorrectCount);
        if (existing is null)
            _conversions.Add(new ScoreConversion(Id, skill, rawCorrectCount, scaledScore));
        else
            existing.SetScaledScore(scaledScore);
    }

    public int GetScore(ExamSkill skill, int rawCorrectCount) => _conversions.Single(conversion =>
        conversion.Skill == skill && conversion.RawCorrectCount == rawCorrectCount).ScaledScore;

    private bool HasAllMappings(ExamSkill skill) => _conversions.Count(conversion => conversion.Skill == skill) == 101 &&
        _conversions.Where(conversion => conversion.Skill == skill).Select(conversion => conversion.RawCorrectCount)
            .Distinct().Count() == 101;
}

public sealed class ScoreConversion
{
    private ScoreConversion()
    {
    }

    internal ScoreConversion(Guid scoreProfileId, ExamSkill skill, int rawCorrectCount, int scaledScore)
    {
        ScoreProfileId = scoreProfileId;
        Skill = skill;
        RawCorrectCount = rawCorrectCount;
        ScaledScore = scaledScore;
    }

    public Guid ScoreProfileId { get; private set; }
    public ExamSkill Skill { get; private set; }
    public int RawCorrectCount { get; private set; }
    public int ScaledScore { get; private set; }

    internal void SetScaledScore(int scaledScore) => ScaledScore = scaledScore;
}

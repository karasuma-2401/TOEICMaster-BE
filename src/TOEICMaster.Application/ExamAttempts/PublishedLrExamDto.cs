namespace TOEICMaster.Application.ExamAttempts;

public sealed record PublishedLrExamDto(
    Guid ExamId,
    string Code,
    string Title,
    int VersionNumber,
    int TotalQuestions,
    int ListeningQuestions,
    int ReadingQuestions,
    DateTimeOffset PublishedAtUtc);

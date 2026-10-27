namespace TOEICMaster.Application.Exams;

public sealed class ExamNotFoundException : Exception;
public sealed class ExamConflictException : Exception;
public sealed class ExamPublicationException(IReadOnlyList<string> errors) : Exception
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
public sealed class InvalidExamMediaException : Exception;

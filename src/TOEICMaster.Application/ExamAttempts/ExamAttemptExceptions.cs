namespace TOEICMaster.Application.ExamAttempts;

public sealed class ExamAttemptNotFoundException(string message) : Exception(message)
{
    public static ExamAttemptNotFoundException ForAttempt(Guid attemptId) =>
        new($"Exam attempt with ID '{attemptId}' was not found.");

    public static ExamAttemptNotFoundException ForExam(Guid examId) =>
        new($"Exam with ID '{examId}' was not found.");
}

public sealed class ExamAttemptConflictException(string message) : Exception(message);

public sealed class ExamAttemptValidationException(string message) : Exception(message);

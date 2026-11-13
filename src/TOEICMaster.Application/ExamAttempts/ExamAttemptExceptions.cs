namespace TOEICMaster.Application.ExamAttempts;

public sealed class ExamAttemptNotFoundException(string message) : Exception(message)
{
    public static ExamAttemptNotFoundException ForAttempt(Guid attemptId) =>
        new($"Không tìm thấy lượt làm bài thi với mã '{attemptId}'.");

    public static ExamAttemptNotFoundException ForExam(Guid examId) =>
        new($"Không tìm thấy đề thi với mã '{examId}'.");
}

public sealed class ExamAttemptConflictException(string message) : Exception(message);

public sealed class ExamAttemptValidationException(string message) : Exception(message);

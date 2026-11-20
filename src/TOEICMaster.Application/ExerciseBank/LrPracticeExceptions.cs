namespace TOEICMaster.Application.ExerciseBank;

public sealed class LrPracticeItemNotFoundException(string message) : Exception(message);

public sealed class InvalidLrPracticeSubmissionException(string message) : Exception(message);

public sealed class InvalidLrPracticeRequestException(string message) : Exception(message);

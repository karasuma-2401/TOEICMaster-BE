namespace TOEICMaster.Application.Writing.Attempts;

public sealed class WritingAttemptNotFoundException : Exception;

public sealed class WritingAttemptConflictException(string message) : Exception(message);

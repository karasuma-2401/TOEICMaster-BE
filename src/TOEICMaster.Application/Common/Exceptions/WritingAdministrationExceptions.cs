namespace TOEICMaster.Application.Common.Exceptions;

public sealed class WritingResourceNotFoundException : Exception;

public sealed class WritingConflictException(string message) : Exception(message);

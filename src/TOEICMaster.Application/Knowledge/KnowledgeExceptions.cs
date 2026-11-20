namespace TOEICMaster.Application.Knowledge;

public sealed class KnowledgeNotFoundException(string message) : Exception(message);

public sealed class KnowledgeConflictException(string message) : Exception(message);

public sealed class InvalidKnowledgeMediaException(string message) : Exception(message);

public sealed class KnowledgeMediaTooLargeException(string message) : Exception(message);

public sealed class InvalidKnowledgeContentException(string message, string field) : Exception(message)
{
    public string Field { get; } = field;
}

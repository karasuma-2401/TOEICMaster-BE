namespace TOEICMaster.Application.Accounts;

public sealed class AdminUserNotFoundException(string message = "User not found.") : Exception(message);

public sealed class AdminUserOperationConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class AdminUserRateLimitException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

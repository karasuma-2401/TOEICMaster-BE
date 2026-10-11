namespace TOEICMaster.Application.Common.Exceptions;

public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string message = "Email already in use.") : base(message)
    {
    }
}

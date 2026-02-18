namespace Hookbase.Exceptions;

/// <summary>
/// Base exception for all Hookbase SDK errors.
/// </summary>
public class HookbaseException : Exception
{
    public HookbaseException(string message) : base(message)
    {
    }

    public HookbaseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

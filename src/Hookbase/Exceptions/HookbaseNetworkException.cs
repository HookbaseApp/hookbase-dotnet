namespace Hookbase.Exceptions;

/// <summary>
/// Thrown when a request could not reach the API or its connection failed before a response
/// arrived (DNS failure, refused or reset connection, TLS error), after any retries. The inner
/// exception is the <see cref="HttpRequestException"/> that caused it.
/// </summary>
/// <remarks>
/// It derives from <see cref="HookbaseException"/>, so code that catches that keeps working.
/// </remarks>
public class HookbaseNetworkException : HookbaseException
{
    public HookbaseNetworkException(string message) : base(message)
    {
    }

    public HookbaseNetworkException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

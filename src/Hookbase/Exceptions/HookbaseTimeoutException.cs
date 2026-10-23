namespace Hookbase.Exceptions;

/// <summary>
/// Thrown when a request did not complete within <see cref="HookbaseClientOptions.Timeout"/>
/// (after any retries). The inner exception is the <see cref="TaskCanceledException"/> that
/// stopped the attempt, whose own inner exception is a <see cref="TimeoutException"/>.
/// </summary>
/// <remarks>
/// It derives from <see cref="HookbaseException"/>, so code that catches that keeps working.
/// A cancellation requested through the caller's own token is not a timeout: it surfaces as an
/// <see cref="OperationCanceledException"/>.
/// </remarks>
public class HookbaseTimeoutException : HookbaseException
{
    public HookbaseTimeoutException(string message) : base(message)
    {
    }

    public HookbaseTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

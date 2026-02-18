namespace Hookbase.Exceptions;

/// <summary>
/// Exception thrown when authentication fails (401).
/// </summary>
public class AuthenticationException : HookbaseApiException
{
    public AuthenticationException(
        string message,
        string? requestId = null,
        Dictionary<string, object>? details = null)
        : base(message, 401, requestId, details)
    {
    }
}

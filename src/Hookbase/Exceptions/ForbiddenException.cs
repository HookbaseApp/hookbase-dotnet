namespace Hookbase.Exceptions;

/// <summary>
/// Exception thrown when access is forbidden (403).
/// </summary>
public class ForbiddenException : HookbaseApiException
{
    public ForbiddenException(
        string message,
        string? requestId = null,
        Dictionary<string, object>? details = null)
        : base(message, 403, requestId, details)
    {
    }
}

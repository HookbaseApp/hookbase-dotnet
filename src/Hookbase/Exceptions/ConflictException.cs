namespace Hookbase.Exceptions;

/// <summary>
/// Exception thrown when there's a conflict with existing data (409).
/// </summary>
public class ConflictException : HookbaseApiException
{
    public ConflictException(
        string message,
        string? requestId = null,
        Dictionary<string, object>? details = null)
        : base(message, 409, requestId, details)
    {
    }
}

namespace Hookbase.Exceptions;

/// <summary>
/// Exception thrown when a resource is not found (404).
/// </summary>
public class NotFoundException : HookbaseApiException
{
    public NotFoundException(
        string message,
        string? requestId = null,
        Dictionary<string, object>? details = null)
        : base(message, 404, requestId, details)
    {
    }
}

namespace Hookbase.Exceptions;

/// <summary>
/// Exception thrown when the API returns an error response.
/// </summary>
public class HookbaseApiException : HookbaseException
{
    /// <summary>
    /// HTTP status code of the error response.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Request ID from the response headers for debugging.
    /// </summary>
    public string? RequestId { get; }

    /// <summary>
    /// Additional error details from the API response.
    /// </summary>
    public Dictionary<string, object>? Details { get; }

    public HookbaseApiException(
        string message,
        int statusCode,
        string? requestId = null,
        Dictionary<string, object>? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        RequestId = requestId;
        Details = details;
    }

    public HookbaseApiException(
        string message,
        int statusCode,
        Exception innerException,
        string? requestId = null,
        Dictionary<string, object>? details = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        RequestId = requestId;
        Details = details;
    }
}

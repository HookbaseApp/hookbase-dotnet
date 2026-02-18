namespace Hookbase.Exceptions;

/// <summary>
/// Exception thrown when rate limit is exceeded (429).
/// </summary>
public class RateLimitException : HookbaseApiException
{
    /// <summary>
    /// Number of seconds to wait before retrying.
    /// </summary>
    public int? RetryAfter { get; }

    public RateLimitException(
        string message,
        int? retryAfter = null,
        string? requestId = null,
        Dictionary<string, object>? details = null)
        : base(message, 429, requestId, details)
    {
        RetryAfter = retryAfter;
    }
}

namespace Hookbase.Exceptions;

/// <summary>
/// Exception thrown when request validation fails (400/422).
/// </summary>
public class ValidationException : HookbaseApiException
{
    /// <summary>
    /// Field-level validation errors.
    /// </summary>
    public Dictionary<string, string[]>? FieldErrors { get; }

    public ValidationException(
        string message,
        int statusCode,
        Dictionary<string, string[]>? fieldErrors = null,
        string? requestId = null,
        Dictionary<string, object>? details = null)
        : base(message, statusCode, requestId, details)
    {
        FieldErrors = fieldErrors;
    }
}

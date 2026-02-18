namespace Hookbase.Exceptions;

/// <summary>
/// Exception thrown when webhook signature verification fails.
/// </summary>
public class WebhookVerificationException : HookbaseException
{
    public WebhookVerificationException(string message) : base(message)
    {
    }

    public WebhookVerificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

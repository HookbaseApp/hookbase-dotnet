namespace Hookbase;

/// <summary>
/// Configuration options for the Hookbase client.
/// </summary>
public class HookbaseClientOptions
{
    /// <summary>
    /// API key for authentication (required). Should start with 'whr_'.
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// Base URL for the Hookbase API.
    /// Default: https://api.hookbase.app
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.hookbase.app";

    /// <summary>
    /// Request timeout in seconds, applied to each attempt (a retry gets a fresh timeout).
    /// It is enforced per request, so it also applies to a supplied <see cref="HttpClient"/>
    /// without changing that client's own <c>Timeout</c>; whichever is shorter wins.
    /// Default: 30 seconds
    /// </summary>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts for failed requests (429, 5xx, network errors and
    /// timeouts). A 429 is retried for every method, honouring <c>Retry-After</c>. Other failures
    /// of a POST are retried only when it carries an idempotency key, as <c>Messages.SendAsync</c>
    /// always does.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Optional HttpClient to use for requests, for example one from <c>IHttpClientFactory</c>.
    /// The SDK never modifies it (<c>BaseAddress</c>, <c>Timeout</c> and
    /// <c>DefaultRequestHeaders</c> are left alone; the URL, API key and User-Agent are set on
    /// each request) and never disposes it, so one instance can be shared by several clients.
    /// If not provided, a new HttpClient is created and disposed with the client.
    /// </summary>
    public HttpClient? HttpClient { get; set; }
}

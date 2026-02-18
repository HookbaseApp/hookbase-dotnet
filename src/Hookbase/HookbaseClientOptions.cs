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
    /// Request timeout in seconds.
    /// Default: 30 seconds
    /// </summary>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts for failed requests.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Optional HttpClient to use for requests.
    /// If not provided, a new HttpClient will be created.
    /// </summary>
    public HttpClient? HttpClient { get; set; }
}

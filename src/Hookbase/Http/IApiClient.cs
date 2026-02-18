namespace Hookbase.Http;

/// <summary>
/// Interface for making HTTP requests to the Hookbase API.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Makes an HTTP request to the API and deserializes the response.
    /// </summary>
    Task<TResponse> RequestAsync<TResponse>(
        HttpMethod method,
        string path,
        object? body = null,
        Dictionary<string, string>? queryParams = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hookbase.Exceptions;

namespace Hookbase.Http;

/// <summary>
/// HTTP client implementation with retry logic and error handling.
/// </summary>
public class ApiClient : IApiClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly int _maxRetries;
    private readonly Random _random = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly HashSet<int> RetryableStatusCodes = new()
    {
        (int)HttpStatusCode.TooManyRequests, // 429
        (int)HttpStatusCode.InternalServerError, // 500
        (int)HttpStatusCode.BadGateway, // 502
        (int)HttpStatusCode.ServiceUnavailable, // 503
        (int)HttpStatusCode.GatewayTimeout // 504
    };

    public ApiClient(string apiKey, string baseUrl, int timeout, int maxRetries, HttpClient? httpClient = null)
    {
        _maxRetries = maxRetries;

        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient();
            _disposeHttpClient = true;
        }

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(timeout);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Hookbase.NET/1.0.0");
    }

    public async Task<TResponse> RequestAsync<TResponse>(
        HttpMethod method,
        string path,
        object? body = null,
        Dictionary<string, string>? queryParams = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(path, queryParams);
        var attempt = 0;

        while (true)
        {
            try
            {
                using var request = new HttpRequestMessage(method, url);

                if (idempotencyKey != null)
                {
                    request.Headers.Add("Idempotency-Key", idempotencyKey);
                }

                if (body != null)
                {
                    request.Content = JsonContent.Create(body, options: JsonOptions);
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var requestId = response.Headers.TryGetValues("X-Request-Id", out var values)
                    ? values.FirstOrDefault()
                    : null;

                if (response.IsSuccessStatusCode)
                {
                    return await DeserializeResponse<TResponse>(response, cancellationToken);
                }

                // Check if we should retry
                var statusCode = (int)response.StatusCode;
                if (attempt < _maxRetries && RetryableStatusCodes.Contains(statusCode))
                {
                    var delay = CalculateDelay(attempt, response);
                    await Task.Delay(delay, cancellationToken);
                    attempt++;
                    continue;
                }

                // Not retrying, throw exception
                await ThrowApiException(response, requestId, cancellationToken);
                throw new InvalidOperationException("Should not reach here");
            }
            catch (Exception ex) when (ex is not HookbaseException)
            {
                if (attempt < _maxRetries && ShouldRetryException(ex))
                {
                    var delay = CalculateDelay(attempt, null);
                    await Task.Delay(delay, cancellationToken);
                    attempt++;
                    continue;
                }

                throw new HookbaseException($"Request failed: {ex.Message}", ex);
            }
        }
    }

    private static async Task<TResponse> DeserializeResponse<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(content, JsonOptions);
            if (result == null)
            {
                throw new HookbaseException("Failed to deserialize response: result is null");
            }
            return result;
        }
        catch (JsonException ex)
        {
            throw new HookbaseException($"Failed to deserialize response: {ex.Message}", ex);
        }
    }

    private static async Task ThrowApiException(
        HttpResponseMessage response,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        string? errorMessage = null;
        Dictionary<string, object>? details = null;
        Dictionary<string, string[]>? fieldErrors = null;

        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrEmpty(content))
            {
                var errorResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content, JsonOptions);
                if (errorResponse != null)
                {
                    if (errorResponse.TryGetValue("message", out var msg))
                    {
                        errorMessage = msg.GetString();
                    }
                    else if (errorResponse.TryGetValue("error", out var err))
                    {
                        errorMessage = err.GetString();
                    }

                    details = errorResponse.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (object)kvp.Value.ToString()!
                    );

                    // Extract field errors for validation exceptions
                    if (errorResponse.TryGetValue("errors", out var errorsElement))
                    {
                        try
                        {
                            fieldErrors = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
                                errorsElement.GetRawText(),
                                JsonOptions
                            );
                        }
                        catch
                        {
                            // Ignore deserialization errors
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore errors parsing error response
        }

        errorMessage ??= $"HTTP {statusCode}: {response.ReasonPhrase}";

        // Throw specific exception based on status code
        switch (statusCode)
        {
            case 400:
            case 422:
                throw new ValidationException(errorMessage, statusCode, fieldErrors, requestId, details);

            case 401:
                throw new AuthenticationException(errorMessage, requestId, details);

            case 403:
                throw new ForbiddenException(errorMessage, requestId, details);

            case 404:
                throw new NotFoundException(errorMessage, requestId, details);

            case 409:
                throw new ConflictException(errorMessage, requestId, details);

            case 429:
                int? retryAfter = null;
                if (response.Headers.RetryAfter?.Delta.HasValue == true)
                {
                    retryAfter = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                }
                throw new RateLimitException(errorMessage, retryAfter, requestId, details);

            default:
                throw new HookbaseApiException(errorMessage, statusCode, requestId, details);
        }
    }

    private TimeSpan CalculateDelay(int attempt, HttpResponseMessage? response)
    {
        // Check for Retry-After header
        if (response?.Headers.RetryAfter?.Delta.HasValue == true)
        {
            return response.Headers.RetryAfter.Delta.Value;
        }

        // Exponential backoff with jitter: min(2^attempt, 10) + jitter
        var baseDelay = Math.Min(Math.Pow(2, attempt), 10);
        var jitter = _random.NextDouble() * 0.3 * baseDelay; // 0-30% jitter
        return TimeSpan.FromSeconds(baseDelay + jitter);
    }

    private static bool ShouldRetryException(Exception ex)
    {
        return ex is HttpRequestException or TaskCanceledException;
    }

    private static string BuildUrl(string path, Dictionary<string, string>? queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
        {
            return path;
        }

        var queryString = string.Join("&", queryParams
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return string.IsNullOrEmpty(queryString) ? path : $"{path}?{queryString}";
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

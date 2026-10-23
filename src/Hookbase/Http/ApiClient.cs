using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hookbase.Exceptions;
using Hookbase.Json;

namespace Hookbase.Http;

/// <summary>
/// HTTP client implementation with retry logic and error handling.
/// </summary>
/// <remarks>
/// A supplied <see cref="HttpClient"/> is never modified: its <c>BaseAddress</c>, <c>Timeout</c>
/// and <c>DefaultRequestHeaders</c> stay as the caller set them. Every request is sent with an
/// absolute URI built from the base URL, carries its own <c>Authorization</c> and
/// <c>User-Agent</c> headers, and is bounded by the SDK timeout through a per-request
/// cancellation token. That makes clients from <c>IHttpClientFactory</c>, and one client shared by
/// several <see cref="ApiClient"/> instances with different API keys, safe to use.
/// </remarks>
public class ApiClient : IApiClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly TimeSpan _timeout;
    private readonly int _maxRetries;
    private readonly Random _random = new();

    private static readonly JsonSerializerOptions JsonOptions = HookbaseJson.Options;

    /// <summary>
    /// The User-Agent sent with every request, <c>Hookbase.NET/&lt;package version&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Read from the assembly rather than written out: it was hardcoded as 1.0.0 through every
    /// release up to 4.1.0, so the API could not tell SDK versions apart. The informational
    /// version is the csproj <c>Version</c>; the SDK appends <c>+&lt;commit&gt;</c> to it when
    /// building from git, which is stripped here.
    /// </remarks>
    internal static readonly string UserAgent = "Hookbase.NET/" + ResolveVersion();

    private static string ResolveVersion()
    {
        var assembly = typeof(ApiClient).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static readonly HashSet<int> RetryableStatusCodes = new()
    {
        (int)HttpStatusCode.TooManyRequests, // 429
        (int)HttpStatusCode.InternalServerError, // 500
        (int)HttpStatusCode.BadGateway, // 502
        (int)HttpStatusCode.ServiceUnavailable, // 503
        (int)HttpStatusCode.GatewayTimeout // 504
    };

    /// <param name="apiKey">API key, sent as <c>Authorization: Bearer</c> on every request.</param>
    /// <param name="baseUrl">Absolute base URL of the API, e.g. <c>https://api.hookbase.app</c>.</param>
    /// <param name="timeout">Per-request timeout in seconds (each retry attempt gets its own).</param>
    /// <param name="maxRetries">Maximum number of retries for a retryable failure.</param>
    /// <param name="httpClient">Optional client to send through. It is not modified and not
    /// disposed. When omitted, the SDK creates (and disposes) its own.</param>
    public ApiClient(string apiKey, string baseUrl, int timeout, int maxRetries, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required", nameof(apiKey));
        }

        if (timeout <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be a positive number of seconds");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new ArgumentException("Base URL must be an absolute URI", nameof(baseUrl));
        }

        _baseUrl = baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        _apiKey = apiKey;
        _timeout = TimeSpan.FromSeconds(timeout);
        _maxRetries = maxRetries;

        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeHttpClient = false;
        }
        else
        {
            // The SDK timeout is enforced per request, so the client's own (100 s default) must
            // not cut a longer one short.
            _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            _disposeHttpClient = true;
        }
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
        var canRetry = IsRetrySafe(method, idempotencyKey);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // One timeout per attempt, linked to the caller's token so either can stop it.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);
            var attemptToken = timeoutCts.Token;

            try
            {
                using var request = new HttpRequestMessage(method, url);

                // Per request, never on DefaultRequestHeaders: the HttpClient may be shared.
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

                if (idempotencyKey != null)
                {
                    request.Headers.Add("Idempotency-Key", idempotencyKey);
                }

                if (body != null)
                {
                    request.Content = JsonContent.Create(body, options: JsonOptions);
                }

                using var response = await _httpClient.SendAsync(request, attemptToken);
                var requestId = response.Headers.TryGetValues("X-Request-Id", out var values)
                    ? values.FirstOrDefault()
                    : null;

                if (response.IsSuccessStatusCode)
                {
                    return await DeserializeResponse<TResponse>(response, attemptToken);
                }

                // Check if we should retry
                var statusCode = (int)response.StatusCode;
                if (canRetry && attempt < _maxRetries && RetryableStatusCodes.Contains(statusCode))
                {
                    var delay = CalculateDelay(attempt, response);
                    await Task.Delay(delay, cancellationToken);
                    attempt++;
                    continue;
                }

                // Not retrying, throw exception
                await ThrowApiException(response, requestId, attemptToken);
                throw new InvalidOperationException("Should not reach here");
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                // The caller cancelled: surface that as a cancellation carrying the caller's token,
                // never retry or wrap it.
                throw new TaskCanceledException(ex.Message, ex, cancellationToken);
            }
            catch (Exception ex) when (ex is not HookbaseException)
            {
                if (ex is OperationCanceledException && timeoutCts.IsCancellationRequested)
                {
                    // Our per-request timeout fired. Report it the way HttpClient.Timeout used to.
                    ex = new TaskCanceledException(
                        $"The request was canceled due to the configured HttpClient.Timeout of {_timeout.TotalSeconds} seconds elapsing.",
                        new TimeoutException(ex.Message, ex));
                }

                if (canRetry && attempt < _maxRetries && ShouldRetryException(ex))
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

        // A string response is the raw body (for example a CSV or JSON export), not a JSON string.
        if (typeof(TResponse) == typeof(string))
        {
            return (TResponse)(object)content;
        }

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

                    // Extract field errors for validation exceptions (Zod's .flatten() shape:
                    // { error, code?, details: { formErrors: string[], fieldErrors: Record<string, string[]> } })
                    if (errorResponse.TryGetValue("details", out var detailsElement) &&
                        detailsElement.ValueKind == JsonValueKind.Object &&
                        detailsElement.TryGetProperty("fieldErrors", out var fieldErrorsElement))
                    {
                        try
                        {
                            fieldErrors = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
                                fieldErrorsElement.GetRawText(),
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

    /// <summary>
    /// Whether a failed request may be sent again without risking a duplicate side effect.
    /// </summary>
    /// <remarks>
    /// A POST that failed with a 5xx or a timeout may still have been processed, so sending it
    /// again could, for example, deliver a webhook twice. POSTs are therefore only retried when
    /// they carry an idempotency key, which lets the API recognise the repeat.
    /// </remarks>
    private static bool IsRetrySafe(HttpMethod method, string? idempotencyKey)
    {
        return method != HttpMethod.Post || !string.IsNullOrEmpty(idempotencyKey);
    }

    private static bool ShouldRetryException(Exception ex)
    {
        return ex is HttpRequestException or TaskCanceledException;
    }

    private Uri BuildUrl(string path, Dictionary<string, string>? queryParams)
    {
        var url = _baseUrl + (path.StartsWith('/') ? path : "/" + path);

        if (queryParams != null && queryParams.Count > 0)
        {
            var queryString = string.Join("&", queryParams
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            if (!string.IsNullOrEmpty(queryString))
            {
                url += "?" + queryString;
            }
        }

        return new Uri(url, UriKind.Absolute);
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

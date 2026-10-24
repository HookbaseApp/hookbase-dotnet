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

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Base URL must be an absolute http(s) URL", nameof(baseUrl));
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

                // Check if we should retry. A 429 is rejected before the API does any work, so it
                // is safe to repeat for any method; 5xx only when the request is retry-safe.
                // A Retry-After longer than MaxRetryAfter (a quota window, or a far-off date) is
                // thrown to the caller as a RateLimitException rather than waited out.
                var statusCode = (int)response.StatusCode;
                var retryable = statusCode == (int)HttpStatusCode.TooManyRequests
                    ? !(RetryAfterDelay(response) > MaxRetryAfter)
                    : canRetry && RetryableStatusCodes.Contains(statusCode);
                if (retryable && attempt < _maxRetries)
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
                    // Our per-request timeout fired. Same exception types HttpClient.Timeout produces.
                    var message = $"Request timed out after {_timeout.TotalSeconds} seconds (HookbaseClientOptions.Timeout)";
                    ex = new TaskCanceledException(message, new TimeoutException(message, ex));
                }

                if (canRetry && attempt < _maxRetries && ShouldRetryException(ex))
                {
                    var delay = CalculateDelay(attempt, null);
                    await Task.Delay(delay, cancellationToken);
                    attempt++;
                    continue;
                }

                // Subclasses of HookbaseException, so a catch of that still sees them.
                throw ex switch
                {
                    TaskCanceledException => new HookbaseTimeoutException($"Request failed: {ex.Message}", ex),
                    HttpRequestException => new HookbaseNetworkException($"Request failed: {ex.Message}", ex),
                    _ => new HookbaseException($"Request failed: {ex.Message}", ex),
                };
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
        string? code = null;

        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrEmpty(content))
            {
                var errorResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content, JsonOptions);
                if (errorResponse != null)
                {
                    // Hookbase sends { error: "<text>", code?, message?, details? }; message, when
                    // present, is the more specific text. Tolerate a nested { error: { message,
                    // code } } too, and never let a non-string value lose the rest of the body.
                    errorResponse.TryGetValue("error", out var err);
                    errorMessage = StringValue(errorResponse, "message")
                        ?? (err.ValueKind == JsonValueKind.String ? err.GetString() : null)
                        ?? (err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var nested)
                            && nested.ValueKind == JsonValueKind.String ? nested.GetString() : null);
                    if (string.IsNullOrEmpty(errorMessage))
                    {
                        errorMessage = null;
                    }

                    code = StringValue(errorResponse, "code")
                        ?? (err.ValueKind == JsonValueKind.Object && err.TryGetProperty("code", out var nestedCode)
                            && nestedCode.ValueKind == JsonValueKind.String ? nestedCode.GetString() : null);

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
        throw statusCode switch
        {
            400 or 422 => new ValidationException(errorMessage, statusCode, fieldErrors, requestId, details) { Code = code },
            401 => new AuthenticationException(errorMessage, requestId, details) { Code = code },
            403 => new ForbiddenException(errorMessage, requestId, details) { Code = code },
            404 => new NotFoundException(errorMessage, requestId, details) { Code = code },
            409 => new ConflictException(errorMessage, requestId, details) { Code = code },
            429 => new RateLimitException(errorMessage, RetryAfterSeconds(response), requestId, details) { Code = code },
            _ => new HookbaseApiException(errorMessage, statusCode, requestId, details) { Code = code },
        };
    }

    private static string? StringValue(Dictionary<string, JsonElement> body, string key) =>
        body.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>
    /// The wait a <c>Retry-After</c> header asks for, as delta-seconds or an HTTP-date
    /// (RFC 9110 10.2.3); null when there is none. A date in the past means no wait.
    /// </summary>
    /// <summary>The longest <c>Retry-After</c> the client waits out before retrying a 429.</summary>
    internal static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(60);

    private static TimeSpan? RetryAfterDelay(HttpResponseMessage? response)
    {
        var header = response?.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    private static int? RetryAfterSeconds(HttpResponseMessage response) =>
        RetryAfterDelay(response) is { } wait ? (int)Math.Ceiling(wait.TotalSeconds) : null;

    private TimeSpan CalculateDelay(int attempt, HttpResponseMessage? response)
    {
        // Honour Retry-After (seconds or an HTTP-date) when the response carries one.
        if (RetryAfterDelay(response) is { } retryAfter)
        {
            return retryAfter;
        }

        // Exponential backoff with jitter: min(2^attempt, 10) + jitter
        var baseDelay = Math.Min(Math.Pow(2, attempt), 10);
        var jitter = Random.Shared.NextDouble() * 0.3 * baseDelay; // 0-30% jitter
        return TimeSpan.FromSeconds(baseDelay + jitter);
    }

    /// <summary>
    /// Whether a failed request may be sent again without risking a duplicate side effect.
    /// </summary>
    /// <remarks>
    /// A POST that failed with a 5xx, a network error or a timeout may still have been processed,
    /// so sending it again could, for example, deliver a webhook twice. Those failures are
    /// therefore only retried for a POST that carries an idempotency key, which lets the API
    /// recognise the repeat. A 429 does not depend on this: the API rejects it before doing any
    /// work, so it is retried for every method.
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

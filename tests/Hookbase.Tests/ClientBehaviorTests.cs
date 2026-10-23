using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Hookbase.Exceptions;
using Hookbase.Http;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Error mapping and the retry policy shared by every SDK: 429 is retried for any method,
/// honouring Retry-After; 5xx, network errors and timeouts only when the request is retry-safe
/// (not a POST, or a POST with an idempotency key); other 4xx never. No request leaves the
/// process.
/// </summary>
public class ClientBehaviorTests
{
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _steps = new();
        public List<(HttpMethod Method, string? IdempotencyKey)> Requests { get; } = new();

        public ScriptedHandler Then(Func<HttpRequestMessage, HttpResponseMessage> step)
        {
            _steps.Enqueue(step);
            return this;
        }

        public ScriptedHandler Then(HttpStatusCode status, string body = "{}", Action<HttpResponseMessage>? configure = null) =>
            Then(_ =>
            {
                var response = new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                configure?.Invoke(response);
                return response;
            });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method,
                request.Headers.TryGetValues("Idempotency-Key", out var k) ? k.Single() : null));
            if (_steps.Count == 0) throw new InvalidOperationException("No response scripted");
            return Task.FromResult(_steps.Dequeue()(request));
        }
    }

    private static ApiClient Client(ScriptedHandler handler, int maxRetries = 3) =>
        new("whr_test", "https://api.test.invalid", 30, maxRetries, new HttpClient(handler));

    private static Task<Dictionary<string, object>> Send(ApiClient client, HttpMethod method, string? idempotencyKey = null) =>
        client.RequestAsync<Dictionary<string, object>>(method, "/api/thing", idempotencyKey: idempotencyKey);

    private static void NoWait(HttpResponseMessage r) => r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);

    // ---- error mapping -------------------------------------------------------------------------

    [Theory]
    [InlineData(400, typeof(ValidationException))]
    [InlineData(422, typeof(ValidationException))]
    [InlineData(401, typeof(AuthenticationException))]
    [InlineData(403, typeof(ForbiddenException))]
    [InlineData(404, typeof(NotFoundException))]
    [InlineData(409, typeof(ConflictException))]
    [InlineData(429, typeof(RateLimitException))]
    [InlineData(402, typeof(HookbaseApiException))]
    [InlineData(413, typeof(HookbaseApiException))]
    [InlineData(500, typeof(HookbaseApiException))]
    [InlineData(503, typeof(HookbaseApiException))]
    public async Task Each_status_maps_to_its_exception_type(int status, Type expected)
    {
        var handler = new ScriptedHandler().Then((HttpStatusCode)status,
            "{\"error\":\"Nope\",\"code\":\"SOME_CODE\"}",
            r => r.Headers.Add("X-Request-Id", "req_123"));
        using var client = Client(handler, maxRetries: 0);

        var ex = await Assert.ThrowsAnyAsync<HookbaseApiException>(() => Send(client, HttpMethod.Get));

        Assert.IsType(expected, ex);
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal("Nope", ex.Message);
        Assert.Equal("SOME_CODE", ex.Code);
        Assert.Equal("req_123", ex.RequestId);
        Assert.Equal("Nope", ex.Details!["error"]);
    }

    [Fact]
    public async Task Message_is_preferred_over_error_when_both_are_sent()
    {
        // e.g. send-event.ts: { error: 'Invalid input', message: 'Idempotency-Key must be at most 255 characters' }
        var handler = new ScriptedHandler().Then(HttpStatusCode.BadRequest,
            "{\"error\":\"Invalid input\",\"message\":\"Idempotency-Key must be at most 255 characters\"}");
        using var client = Client(handler, 0);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Send(client, HttpMethod.Post));
        Assert.Equal("Idempotency-Key must be at most 255 characters", ex.Message);
        Assert.Null(ex.Code);
    }

    [Fact]
    public async Task A_nested_error_object_still_yields_message_and_code()
    {
        var handler = new ScriptedHandler().Then(HttpStatusCode.Conflict,
            "{\"error\":{\"message\":\"Slug taken\",\"code\":\"DUPLICATE\"}}");
        using var client = Client(handler, 0);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Send(client, HttpMethod.Get));
        Assert.Equal("Slug taken", ex.Message);
        Assert.Equal("DUPLICATE", ex.Code);
        Assert.NotNull(ex.Details);
    }

    [Fact]
    public async Task A_non_string_message_does_not_lose_the_rest_of_the_body()
    {
        var handler = new ScriptedHandler().Then(HttpStatusCode.BadRequest,
            "{\"message\":{\"x\":1},\"error\":\"Bad thing\",\"code\":\"VALIDATION_ERROR\"}");
        using var client = Client(handler, 0);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Send(client, HttpMethod.Get));
        Assert.Equal("Bad thing", ex.Message);
        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    [Fact]
    public async Task Zod_field_errors_are_exposed_on_ValidationException()
    {
        var handler = new ScriptedHandler().Then(HttpStatusCode.BadRequest,
            "{\"error\":\"Invalid input\",\"details\":{\"formErrors\":[],\"fieldErrors\":{\"url\":[\"Invalid url\"],\"name\":[\"Required\",\"Too short\"]}}}");
        using var client = Client(handler, 0);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Send(client, HttpMethod.Post));
        Assert.Equal("Invalid input", ex.Message);
        Assert.Equal(new[] { "Invalid url" }, ex.FieldErrors!["url"]);
        Assert.Equal(new[] { "Required", "Too short" }, ex.FieldErrors["name"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>Bad gateway</html>")]
    [InlineData("[1,2]")]
    public async Task A_body_that_is_not_a_JSON_object_falls_back_to_the_status_line(string body)
    {
        var handler = new ScriptedHandler().Then(HttpStatusCode.BadGateway, body);
        using var client = Client(handler, 0);

        var ex = await Assert.ThrowsAsync<HookbaseApiException>(() => Send(client, HttpMethod.Get));
        Assert.Equal(502, ex.StatusCode);
        Assert.Equal("HTTP 502: Bad Gateway", ex.Message);
        Assert.Null(ex.Code);
    }

    [Fact]
    public async Task RateLimitException_carries_Retry_After_seconds()
    {
        var handler = new ScriptedHandler().Then(HttpStatusCode.TooManyRequests,
            "{\"error\":\"Rate limit exceeded. Please slow down.\"}",
            r => r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(42)));
        using var client = Client(handler, 0);

        var ex = await Assert.ThrowsAsync<RateLimitException>(() => Send(client, HttpMethod.Get));
        Assert.Equal(42, ex.RetryAfter);
        Assert.Equal("Rate limit exceeded. Please slow down.", ex.Message);
    }

    [Fact]
    public async Task RateLimitException_reads_an_HTTP_date_Retry_After()
    {
        var handler = new ScriptedHandler().Then(HttpStatusCode.TooManyRequests, "{\"error\":\"slow\"}",
            r => r.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(30)));
        using var client = Client(handler, 0);

        var ex = await Assert.ThrowsAsync<RateLimitException>(() => Send(client, HttpMethod.Get));
        Assert.InRange(ex.RetryAfter!.Value, 25, 31);
    }

    [Fact]
    public async Task RateLimitException_without_Retry_After_has_null_RetryAfter()
    {
        var handler = new ScriptedHandler().Then(HttpStatusCode.TooManyRequests, "{\"error\":\"Monthly event limit exceeded\"}");
        using var client = Client(handler, 0);

        var ex = await Assert.ThrowsAsync<RateLimitException>(() => Send(client, HttpMethod.Get));
        Assert.Null(ex.RetryAfter);
    }

    [Fact]
    public async Task A_response_that_is_not_the_expected_JSON_throws_HookbaseException()
    {
        var handler = new ScriptedHandler().Then(HttpStatusCode.OK, "not json");
        using var client = Client(handler, 0);

        var ex = await Assert.ThrowsAsync<HookbaseException>(() => Send(client, HttpMethod.Get));
        Assert.StartsWith("Failed to deserialize response", ex.Message);
    }

    [Fact]
    public async Task A_string_response_is_returned_raw()
    {
        var handler = new ScriptedHandler().Then(HttpStatusCode.OK, "id,name\n1,a\n");
        using var client = Client(handler, 0);

        Assert.Equal("id,name\n1,a\n", await client.RequestAsync<string>(HttpMethod.Get, "/api/export"));
    }

    // ---- retry policy --------------------------------------------------------------------------

    public static TheoryData<string> AllMethods => new() { "GET", "POST", "PUT", "PATCH", "DELETE" };

    [Theory]
    [MemberData(nameof(AllMethods))]
    public async Task A_429_is_retried_for_every_method(string method)
    {
        var handler = new ScriptedHandler()
            .Then(HttpStatusCode.TooManyRequests, "{\"error\":\"slow\"}", NoWait)
            .Then(HttpStatusCode.OK, "{\"ok\":true}");
        using var client = Client(handler);

        await Send(client, new HttpMethod(method));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task A_429_waits_Retry_After_instead_of_the_backoff()
    {
        // The backoff before the first retry is at least 1 s; Retry-After: 0 must win over it.
        var handler = new ScriptedHandler()
            .Then(HttpStatusCode.TooManyRequests, "{}", NoWait)
            .Then(HttpStatusCode.OK, "{}");
        using var client = Client(handler);

        var started = DateTime.UtcNow;
        await Send(client, HttpMethod.Get);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromMilliseconds(800));
    }

    [Fact]
    public async Task A_429_with_Retry_After_over_60s_is_thrown_at_once()
    {
        var handler = new ScriptedHandler()
            .Then(HttpStatusCode.TooManyRequests, "{\"error\":\"Monthly limit\"}",
                r => r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3600)))
            .Then(HttpStatusCode.OK, "{}");
        using var client = Client(handler);

        var started = DateTime.UtcNow;
        var ex = await Assert.ThrowsAsync<RateLimitException>(() => Send(client, HttpMethod.Get));
        Assert.Equal(3600, ex.RetryAfter);
        Assert.Single(handler.Requests);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_429_waits_a_Retry_After_date_until_it_passes()
    {
        var handler = new ScriptedHandler()
            .Then(HttpStatusCode.TooManyRequests, "{}",
                r => r.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(-5)))
            .Then(HttpStatusCode.TooManyRequests, "{}",
                r => r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)))
            .Then(HttpStatusCode.OK, "{}");
        using var client = Client(handler);

        var started = DateTime.UtcNow;
        await Send(client, HttpMethod.Get);
        var elapsed = DateTime.UtcNow - started;
        // A past date means no wait; then exactly the 1 s asked for (the backoff would be 2-2.6 s).
        Assert.InRange(elapsed, TimeSpan.FromMilliseconds(900), TimeSpan.FromMilliseconds(1900));
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task A_429_without_Retry_After_backs_off_exponentially()
    {
        var handler = new ScriptedHandler()
            .Then(HttpStatusCode.TooManyRequests, "{}")
            .Then(HttpStatusCode.OK, "{}");
        using var client = Client(handler);

        var started = DateTime.UtcNow;
        await Send(client, HttpMethod.Post);
        Assert.InRange(DateTime.UtcNow - started, TimeSpan.FromMilliseconds(950), TimeSpan.FromSeconds(3));
    }

    [Theory]
    [InlineData("GET", 500)]
    [InlineData("GET", 502)]
    [InlineData("PUT", 503)]
    [InlineData("PATCH", 504)]
    [InlineData("DELETE", 500)]
    public async Task A_5xx_is_retried_for_a_retry_safe_method(string method, int status)
    {
        var handler = new ScriptedHandler()
            .Then((HttpStatusCode)status, "{}", NoWait)
            .Then(HttpStatusCode.OK, "{}");
        using var client = Client(handler);

        await Send(client, new HttpMethod(method));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task A_POST_with_an_idempotency_key_is_retried_on_5xx_with_the_same_key()
    {
        var handler = new ScriptedHandler()
            .Then(HttpStatusCode.ServiceUnavailable, "{}", NoWait)
            .Then(HttpStatusCode.OK, "{}");
        using var client = Client(handler);

        await Send(client, HttpMethod.Post, idempotencyKey: "key-1");
        Assert.Equal(new[] { "key-1", "key-1" }, handler.Requests.Select(r => r.IdempotencyKey));
    }

    [Fact]
    public async Task A_POST_without_an_idempotency_key_is_not_retried_on_5xx()
    {
        var handler = new ScriptedHandler().Then(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}", NoWait);
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<HookbaseApiException>(() => Send(client, HttpMethod.Post));
        Assert.Equal(500, ex.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(501)]
    public async Task Other_statuses_are_never_retried(int status)
    {
        var handler = new ScriptedHandler().Then((HttpStatusCode)status, "{\"error\":\"x\"}", NoWait);
        using var client = Client(handler);

        await Assert.ThrowsAnyAsync<HookbaseApiException>(() => Send(client, HttpMethod.Get));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Retries_stop_after_MaxRetries_and_throw_the_last_error()
    {
        var handler = new ScriptedHandler();
        for (var i = 0; i < 3; i++) handler.Then(HttpStatusCode.BadGateway, "{\"error\":\"down\"}", NoWait);
        using var client = Client(handler, maxRetries: 2);

        var ex = await Assert.ThrowsAsync<HookbaseApiException>(() => Send(client, HttpMethod.Get));
        Assert.Equal(502, ex.StatusCode);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task MaxRetries_zero_sends_once()
    {
        var handler = new ScriptedHandler().Then(HttpStatusCode.TooManyRequests, "{}", NoWait);
        using var client = Client(handler, maxRetries: 0);

        await Assert.ThrowsAsync<RateLimitException>(() => Send(client, HttpMethod.Get));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_network_error_is_retried_for_GET_then_surfaces_as_HookbaseNetworkException()
    {
        var handler = new ScriptedHandler()
            .Then(_ => throw new HttpRequestException("Connection refused"))
            .Then(_ => throw new HttpRequestException("Connection refused"));
        using var client = Client(handler, maxRetries: 1);

        var ex = await Assert.ThrowsAsync<HookbaseNetworkException>(() => Send(client, HttpMethod.Get));
        Assert.Equal("Request failed: Connection refused", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task A_network_error_on_a_POST_without_a_key_is_not_retried()
    {
        var handler = new ScriptedHandler().Then(_ => throw new HttpRequestException("reset"));
        using var client = Client(handler, maxRetries: 3);

        await Assert.ThrowsAsync<HookbaseNetworkException>(() => Send(client, HttpMethod.Post));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_network_error_then_success_returns_the_response()
    {
        var handler = new ScriptedHandler()
            .Then(_ => throw new HttpRequestException("reset"))
            .Then(HttpStatusCode.OK, "{\"ok\":true}");
        using var client = Client(handler, maxRetries: 1);

        var result = await Send(client, HttpMethod.Post, idempotencyKey: "k");
        Assert.True(result.ContainsKey("ok"));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task New_exception_types_are_HookbaseExceptions()
    {
        Assert.IsAssignableFrom<HookbaseException>(new HookbaseTimeoutException("t"));
        Assert.IsAssignableFrom<HookbaseException>(new HookbaseNetworkException("n"));

        var handler = new ScriptedHandler().Then(_ => throw new HttpRequestException("x"));
        using var client = Client(handler, 0);
        await Assert.ThrowsAnyAsync<HookbaseException>(() => Send(client, HttpMethod.Get));
    }

    // ---- client construction -------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_API_key_is_rejected(string key)
    {
        Assert.Throws<ArgumentException>(() => new HookbaseClient(key));
        Assert.Throws<ArgumentException>(() => new ApiClient(key, "https://api.test.invalid", 30, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_non_positive_timeout_is_rejected(int timeout)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HookbaseClient(new HookbaseClientOptions { ApiKey = "whr_x", Timeout = timeout }));
    }

    [Fact]
    public void Options_have_the_documented_defaults()
    {
        var options = new HookbaseClientOptions { ApiKey = "whr_x" };
        Assert.Equal("https://api.hookbase.app", options.BaseUrl);
        Assert.Equal(30, options.Timeout);
        Assert.Equal(3, options.MaxRetries);
        Assert.Null(options.HttpClient);
    }

    [Fact]
    public async Task User_agent_carries_the_package_version()
    {
        string? userAgent = null;
        var handler = new ScriptedHandler().Then(r =>
        {
            userAgent = string.Join(" ", r.Headers.GetValues("User-Agent"));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        using var client = Client(handler, 0);
        await Send(client, HttpMethod.Get);

        var version = typeof(HookbaseClient).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Single().InformationalVersion.Split('+')[0];
        Assert.Matches(@"^\d+\.\d+\.\d+", version);
        Assert.Equal("Hookbase.NET/" + version, userAgent);
    }
}

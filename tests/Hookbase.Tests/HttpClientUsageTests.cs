using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Hookbase.Exceptions;
using Hookbase.Http;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// A supplied HttpClient (IHttpClientFactory, or one shared by several clients) must not be
/// mutated, and auth/User-Agent/timeout must be applied per request instead.
/// </summary>
public class HttpClientUsageTests
{
    private sealed record Seen(Uri Uri, string[] Authorization, string[] UserAgent);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Seen> Requests { get; } = new();
        public TimeSpan Delay { get; init; } = TimeSpan.Zero;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add(new Seen(
                    request.RequestUri!,
                    request.Headers.TryGetValues("Authorization", out var a) ? a.ToArray() : Array.Empty<string>(),
                    request.Headers.TryGetValues("User-Agent", out var u) ? u.ToArray() : Array.Empty<string>()));
            }

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
        }
    }

    private static Task<Dictionary<string, object>> Get(ApiClient client, CancellationToken ct = default) =>
        client.RequestAsync<Dictionary<string, object>>(HttpMethod.Get, "/api/ping",
            queryParams: new Dictionary<string, string> { ["a"] = "b c" }, cancellationToken: ct);

    [Fact]
    public async Task A_supplied_HttpClient_is_not_modified()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
        http.DefaultRequestHeaders.Add("X-Caller", "mine");

        using var client = new ApiClient("whr_one", "https://api.test.invalid", 30, 0, http);
        await Get(client);
        await Get(client);

        Assert.Null(http.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(120), http.Timeout);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
        Assert.Empty(http.DefaultRequestHeaders.UserAgent);
        Assert.Single(http.DefaultRequestHeaders);
        Assert.Equal("mine", http.DefaultRequestHeaders.GetValues("X-Caller").Single());

        Assert.All(handler.Requests, r =>
        {
            Assert.Equal("https://api.test.invalid/api/ping?a=b%20c", r.Uri.AbsoluteUri);
            Assert.Equal(new[] { "Bearer whr_one" }, r.Authorization);
            Assert.StartsWith("Hookbase.NET/", Assert.Single(r.UserAgent));
        });
    }

    [Fact]
    public async Task A_BaseAddress_on_the_supplied_client_is_ignored_and_kept()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://other.example/") };

        using var client = new ApiClient("whr_one", "https://api.test.invalid", 30, 0, http);
        await Get(client);

        Assert.Equal(new Uri("https://other.example/"), http.BaseAddress);
        Assert.Equal("api.test.invalid", handler.Requests.Single().Uri.Host);
    }

    [Fact]
    public async Task Two_clients_sharing_one_HttpClient_each_send_only_their_own_key()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);

        using var one = new ApiClient("whr_one", "https://api.test.invalid", 30, 0, http);
        using var two = new ApiClient("whr_two", "https://api.test.invalid", 30, 0, http);

        await Task.WhenAll(Get(one), Get(two), Get(one), Get(two));

        Assert.Equal(4, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Single(r.Authorization));
        Assert.Equal(2, handler.Requests.Count(r => r.Authorization[0] == "Bearer whr_one"));
        Assert.Equal(2, handler.Requests.Count(r => r.Authorization[0] == "Bearer whr_two"));
        Assert.All(handler.Requests, r => Assert.Single(r.UserAgent));
    }

    [Fact]
    public async Task Default_auth_on_the_supplied_client_does_not_duplicate_the_header()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "caller_default");

        using var client = new ApiClient("whr_one", "https://api.test.invalid", 30, 0, http);
        await Get(client);

        Assert.Equal(new[] { "Bearer whr_one" }, handler.Requests.Single().Authorization);
    }

    [Fact]
    public async Task The_per_request_timeout_fires_on_a_supplied_client()
    {
        var handler = new RecordingHandler { Delay = TimeSpan.FromSeconds(30) };
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };

        using var client = new ApiClient("whr_one", "https://api.test.invalid", 1, 0, http);
        var started = DateTime.UtcNow;
        var ex = await Assert.ThrowsAsync<HookbaseTimeoutException>(() => Get(client));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
        Assert.StartsWith("Request failed: ", ex.Message);
        Assert.Equal("Request failed: Request timed out after 1 seconds (HookbaseClientOptions.Timeout)", ex.Message);
        var inner = Assert.IsType<TaskCanceledException>(ex.InnerException);
        Assert.IsType<TimeoutException>(inner.InnerException);
        Assert.Equal(Timeout.InfiniteTimeSpan, http.Timeout);
    }

    [Fact]
    public async Task A_timed_out_GET_is_retried_with_a_fresh_timeout()
    {
        var handler = new RecordingHandler { Delay = TimeSpan.FromSeconds(30) };
        using var client = new ApiClient("whr_one", "https://api.test.invalid", 1, 1, new HttpClient(handler));

        // Backoff before the retry is ~1-1.3 s, so the whole call is about 3.3 s at most.
        await Assert.ThrowsAsync<HookbaseTimeoutException>(() => Get(client));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Caller_cancellation_surfaces_as_cancellation_and_is_not_retried()
    {
        var handler = new RecordingHandler { Delay = TimeSpan.FromSeconds(30) };
        using var client = new ApiClient("whr_one", "https://api.test.invalid", 30, 3, new HttpClient(handler));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Get(client, cts.Token));
        Assert.Equal(cts.Token, ex.CancellationToken);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task An_already_cancelled_token_throws_cancellation()
    {
        var handler = new RecordingHandler();
        using var client = new ApiClient("whr_one", "https://api.test.invalid", 30, 3, new HttpClient(handler));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Get(client, cts.Token));
    }

    [Fact]
    public async Task Disposing_does_not_dispose_a_supplied_client()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);

        new ApiClient("whr_one", "https://api.test.invalid", 30, 0, http).Dispose();
        new HookbaseClient(new HookbaseClientOptions { ApiKey = "whr_one", BaseUrl = "https://api.test.invalid", HttpClient = http }).Dispose();

        // Still usable.
        using var client = new ApiClient("whr_one", "https://api.test.invalid", 30, 0, http);
        await Get(client);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("api.hookbase.app")]
    [InlineData("/api")]
    [InlineData("ftp://api.hookbase.app")]
    public void A_bad_base_URL_throws_ArgumentException(string baseUrl)
    {
        Assert.Throws<ArgumentException>(() => new ApiClient("whr_one", baseUrl, 30, 0, new HttpClient(new RecordingHandler())));
        Assert.Throws<ArgumentException>(() => new HookbaseClient(new HookbaseClientOptions { ApiKey = "whr_one", BaseUrl = baseUrl }));
    }

    [Fact]
    public async Task A_base_URL_path_prefix_and_trailing_slash_are_kept()
    {
        var handler = new RecordingHandler();
        using var client = new ApiClient("whr_one", "https://proxy.test.invalid/hookbase/", 30, 0, new HttpClient(handler));
        await Get(client);
        Assert.Equal("https://proxy.test.invalid/hookbase/api/ping?a=b%20c", handler.Requests.Single().Uri.AbsoluteUri);
    }
}

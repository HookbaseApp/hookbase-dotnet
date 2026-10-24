using System.Net;
using System.Text;
using System.Text.Json;

namespace Hookbase.Tests;

/// <summary>
/// Shared fake transport: records every request (method, path+query, body) and answers with the
/// next queued response. No request leaves the process.
/// </summary>
internal sealed class TestTransport : HttpMessageHandler
{
    internal sealed record Recorded(HttpMethod Method, string Path, string Query, string? Body)
    {
        public JsonElement Json => JsonDocument.Parse(Body!).RootElement.Clone();
    }

    private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();
    public List<Recorded> Requests { get; } = new();
    public Recorded Last => Requests[^1];

    public TestTransport Respond(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue((status, body));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new Recorded(request.Method, request.RequestUri!.AbsolutePath,
            Uri.UnescapeDataString(request.RequestUri.Query), body));
        if (_responses.Count == 0) throw new InvalidOperationException("No response queued for " + request.RequestUri);
        var (status, text) = _responses.Dequeue();
        return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    }

    public static (HookbaseClient Client, TestTransport Transport) Create()
    {
        var transport = new TestTransport();
        var client = new HookbaseClient(new HookbaseClientOptions
        {
            ApiKey = "whr_test",
            BaseUrl = "https://api.test",
            MaxRetries = 0,
            HttpClient = new HttpClient(transport)
        });
        return (client, transport);
    }
}

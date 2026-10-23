using System.Net;
using System.Text;
using System.Text.Json;
using Hookbase.Exceptions;
using Hookbase.Http;
using Hookbase.Json;
using Hookbase.Models.Endpoints;
using Hookbase.Models.EventTypes;
using Hookbase.Models.Messages;
using Hookbase.Resources;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Requests and response mapping for the outbound surface (send-event, outbound-messages, DLQ,
/// endpoint stats), checked against api/src/routes/send-event.ts and outbound-messages.ts.
/// No request leaves the process: a fake handler records each one and returns a canned response.
/// </summary>
public class OutboundContractTests
{
    private sealed record Recorded(HttpMethod Method, string PathAndQuery, string? Body, string? IdempotencyKeyHeader);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();
        public List<Recorded> Requests { get; } = new();

        public FakeHandler Respond(HttpStatusCode status, string body, string contentType = "application/json")
        {
            _responses.Enqueue(() =>
            {
                var response = new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, contentType)
                };
                // Zero Retry-After keeps retry tests from sleeping through the backoff.
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return response;
            });
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
            Requests.Add(new Recorded(request.Method, request.RequestUri!.PathAndQuery, body, key));
            if (_responses.Count == 0) throw new InvalidOperationException("No response queued for " + request.RequestUri);
            return _responses.Dequeue()();
        }
    }

    private static (ApiClient Client, FakeHandler Handler) Create(int maxRetries = 0)
    {
        var handler = new FakeHandler();
        var client = new ApiClient("whr_test", "https://api.test.invalid", 30, maxRetries, new HttpClient(handler));
        return (client, handler);
    }

    private static JsonElement Json(string? body) => JsonDocument.Parse(body!).RootElement.Clone();

    private const string SendResponse = """
    { "data": { "eventId": "evt_1", "messagesQueued": 2,
                "endpoints": [{ "id": "ep_1", "url": "https://a.example" }, { "id": "ep_2", "url": "https://b.example" }] } }
    """;

    // ---- send-event ----------------------------------------------------------------------------

    [Fact]
    public async Task Send_PostsTheApiFieldsAndMapsTheResponse()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.Created, SendResponse);

        var result = await new MessagesResource(client).SendAsync("org_1", new SendMessageRequest
        {
            EventType = "order.created",
            Payload = new Dictionary<string, object> { ["order_id"] = "o1" },
            ApplicationId = "app_1",
            IdempotencyKey = "key-1",
            Labels = new Dictionary<string, string> { ["region"] = "eu" },
            Metadata = new Dictionary<string, object> { ["source"] = "test" },
            Priority = 1
        });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/organizations/org_1/send-event", request.PathAndQuery);
        Assert.Equal("key-1", request.IdempotencyKeyHeader);

        var body = Json(request.Body);
        Assert.Equal("order.created", body.GetProperty("eventType").GetString());
        Assert.Equal("o1", body.GetProperty("payload").GetProperty("order_id").GetString());
        Assert.Equal("app_1", body.GetProperty("applicationId").GetString());
        Assert.Equal("key-1", body.GetProperty("idempotencyKey").GetString());
        Assert.Equal("eu", body.GetProperty("labels").GetProperty("region").GetString());
        Assert.Equal("test", body.GetProperty("metadata").GetProperty("source").GetString());
        Assert.Equal(1, body.GetProperty("priority").GetInt32());
        Assert.False(body.TryGetProperty("endpointId", out _));
        Assert.False(body.TryGetProperty("endpointIds", out _));
        Assert.False(body.TryGetProperty("eventId", out _));

        Assert.Equal("evt_1", result.EventId);
        Assert.Equal(2, result.MessagesQueued);
        Assert.Equal(new[] { "ep_1", "ep_2" }, result.Endpoints!.Select(e => e.Id));
        Assert.Equal("https://b.example", result.Endpoints![1].Url);
#pragma warning disable CS0618
        Assert.Equal("evt_1", result.MessageId);
#pragma warning restore CS0618
    }

    [Fact]
    public async Task Send_WithOneEndpoint_SendsBothTheSingularAndPluralField()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.Created, SendResponse);

        await new MessagesResource(client).SendAsync("org_1", new SendMessageRequest
        {
            EventType = "order.created",
            Payload = new Dictionary<string, object>(),
            EndpointIds = new List<string> { "ep_1" }
        });

        var body = Json(handler.Requests[0].Body);
        Assert.Equal("ep_1", body.GetProperty("endpointId").GetString());
        Assert.Equal(new[] { "ep_1" }, body.GetProperty("endpointIds").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Send_WithSeveralEndpoints_SendsOnlyThePluralField()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.Created, SendResponse);

        await new MessagesResource(client).SendAsync("org_1", new SendMessageRequest
        {
            EventType = "order.created",
            Payload = new Dictionary<string, object>(),
            EndpointIds = new List<string> { "ep_1", "ep_2" },
            EndpointId = "ep_1"
        });

        var body = Json(handler.Requests[0].Body);
        Assert.False(body.TryGetProperty("endpointId", out _));
        Assert.Equal(new[] { "ep_1", "ep_2" }, body.GetProperty("endpointIds").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Send_WithoutAKey_GeneratesOneAndKeepsItAcrossRetries()
    {
        var (client, handler) = Create(maxRetries: 2);
        handler.Respond(HttpStatusCode.ServiceUnavailable, "{\"error\":\"unavailable\"}")
               .Respond(HttpStatusCode.Created, SendResponse);

        await new MessagesResource(client).SendAsync("org_1", new SendMessageRequest
        {
            EventType = "order.created",
            Payload = new Dictionary<string, object>()
        });

        Assert.Equal(2, handler.Requests.Count);
        var first = handler.Requests[0];
        var second = handler.Requests[1];
        Assert.False(string.IsNullOrEmpty(first.IdempotencyKeyHeader));
        Assert.Equal(first.IdempotencyKeyHeader, second.IdempotencyKeyHeader);
        Assert.Equal(first.IdempotencyKeyHeader, Json(first.Body).GetProperty("idempotencyKey").GetString());
        Assert.Equal(first.Body, second.Body);
    }

    [Fact]
    public async Task Send_GeneratesADifferentKeyForEachCall()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.Created, SendResponse).Respond(HttpStatusCode.Created, SendResponse);
        var messages = new MessagesResource(client);
        var request = new SendMessageRequest { EventType = "a.b", Payload = new Dictionary<string, object>() };

        await messages.SendAsync("org_1", request);
        await messages.SendAsync("org_1", request);

        Assert.NotEqual(handler.Requests[0].IdempotencyKeyHeader, handler.Requests[1].IdempotencyKeyHeader);
    }

    [Fact]
    public async Task Send_UsesTheObsoleteEventIdAsTheKey()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.Created, SendResponse);

#pragma warning disable CS0618
        await new MessagesResource(client).SendAsync("org_1", new SendMessageRequest
        {
            EventType = "a.b",
            Payload = new Dictionary<string, object>(),
            EventId = "legacy-key"
        });
#pragma warning restore CS0618

        Assert.Equal("legacy-key", handler.Requests[0].IdempotencyKeyHeader);
        var body = Json(handler.Requests[0].Body);
        Assert.Equal("legacy-key", body.GetProperty("idempotencyKey").GetString());
        Assert.False(body.TryGetProperty("eventId", out _));
    }

    // ---- retry policy --------------------------------------------------------------------------

    [Fact]
    public async Task PostWithoutAKey_IsNotRetriedOn5xx()
    {
        var (client, handler) = Create(maxRetries: 3);
        handler.Respond(HttpStatusCode.ServiceUnavailable, "{\"error\":\"unavailable\"}");

        await Assert.ThrowsAsync<HookbaseApiException>(() => new DlqResource(client).RetryAsync("msg_1"));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Get_IsStillRetriedOn5xx()
    {
        var (client, handler) = Create(maxRetries: 3);
        handler.Respond(HttpStatusCode.BadGateway, "{}")
               .Respond(HttpStatusCode.OK, "{\"data\":{\"total\":0,\"byReason\":{},\"byEndpoint\":[],\"byEventType\":[]}}");

        await new DlqResource(client).GetStatsAsync();
        Assert.Equal(2, handler.Requests.Count);
    }

    // ---- messages ------------------------------------------------------------------------------

    [Fact]
    public async Task List_MapsTheApiFieldsAndPassesTheCursorBackVerbatim()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.OK, """
        { "data": [{
            "id": "msg_1", "eventId": "evt_1", "eventType": "order.created",
            "applicationId": "app_1", "endpointId": "ep_1", "status": "awaiting_retry",
            "attempts": 2, "maxAttempts": 5, "lastResponseStatus": 503,
            "lastErrorMessage": "Service Unavailable", "createdAt": "2026-10-01T00:00:00.000Z",
            "completedAt": null, "endpointUrl": "https://a.example", "applicationName": "Acme" }],
          "pagination": { "hasMore": true, "nextCursor": "2026-10-01T00:00:00.000Z|msg_1" } }
        """)
        .Respond(HttpStatusCode.OK, """{ "data": [], "pagination": { "hasMore": false, "nextCursor": null } }""");

        var messages = new MessagesResource(client);
        var page = await messages.ListAsync("org_1", limit: 1);

        var message = Assert.Single(page.Data);
        Assert.Equal("evt_1", message.EventId);
        Assert.Equal("awaiting_retry", message.Status);
        Assert.Equal(2, message.Attempts);
        Assert.Equal(5, message.MaxAttempts);
        Assert.Equal(503, message.LastResponseStatus);
        Assert.Equal("Service Unavailable", message.LastErrorMessage);
        Assert.Equal("app_1", message.ApplicationId);
        Assert.Equal("Acme", message.ApplicationName);
        Assert.Equal("https://a.example", message.EndpointUrl);
        Assert.Null(message.CompletedAt);
#pragma warning disable CS0618
        Assert.Equal("evt_1", message.MessageId);
        Assert.Equal("Service Unavailable", message.LastError);
#pragma warning restore CS0618
        Assert.True(page.HasMore);

        var next = await messages.ListAsync("org_1", limit: 1, cursor: page.NextCursor);
        Assert.False(next.HasMore);
        Assert.Equal(
            "/api/organizations/org_1/outbound-messages?limit=1&cursor=" + Uri.EscapeDataString("2026-10-01T00:00:00.000Z|msg_1"),
            handler.Requests[1].PathAndQuery);
    }

    [Fact]
    public async Task Get_MapsTheDetailFields()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.OK, """
        { "data": {
            "id": "msg_1", "eventId": "evt_1", "eventType": "order.created", "applicationId": "app_1",
            "endpointId": "ep_1", "subscriptionId": "sub_1", "eventTypeId": "et_1", "status": "dlq",
            "attempts": 5, "maxAttempts": 5, "lastResponseStatus": 500, "lastErrorType": "http_error",
            "lastErrorMessage": "boom", "payloadKey": "k", "payloadSizeBytes": 42, "payloadHash": "h",
            "metadata": { "replayedFrom": "msg_0" }, "priority": 2, "createdAt": "c", "completedAt": "d",
            "endpointUrl": "https://a.example", "endpointDescription": "Primary", "applicationName": "Acme",
            "payload": { "order_id": "o1" } } }
        """);

        var message = await new MessagesResource(client).GetAsync("org_1", "msg_1");

        Assert.Equal("/api/organizations/org_1/outbound-messages/msg_1", handler.Requests[0].PathAndQuery);
        Assert.Equal("sub_1", message.SubscriptionId);
        Assert.Equal("et_1", message.EventTypeId);
        Assert.Equal("http_error", message.LastErrorType);
        Assert.Equal(42, message.PayloadSizeBytes);
        Assert.Equal("h", message.PayloadHash);
        Assert.Equal("msg_0", message.Metadata!.Value.GetProperty("replayedFrom").GetString());
        Assert.Equal(2, message.Priority);
        Assert.Equal("d", message.CompletedAt);
        Assert.Equal("Primary", message.EndpointDescription);
        Assert.Equal("o1", message.Payload!.Value.GetProperty("order_id").GetString());
    }

    [Fact]
    public async Task Get_AcceptsANullPayload()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.OK, """{ "data": { "id": "msg_1", "status": "success", "attempts": 1, "maxAttempts": 5, "payload": null, "metadata": null } }""");

        var message = await new MessagesResource(client).GetAsync("org_1", "msg_1");
        Assert.Null(message.Payload);
        Assert.Null(message.Metadata);
    }

    [Fact]
    public async Task StatsSummary_MapsEveryStatusBucket()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.OK, """
        { "data": { "pending": 1, "processing": 2, "success": 30, "failed": 4, "awaitingRetry": 5,
                    "exhausted": 6, "dlq": 7, "total": 55 } }
        """);

        var stats = await new MessagesResource(client).GetStatsSummaryAsync("org_1");

        Assert.Equal("/api/organizations/org_1/outbound-messages/stats/summary", handler.Requests[0].PathAndQuery);
        Assert.Equal(1, stats.Pending);
        Assert.Equal(2, stats.Processing);
        Assert.Equal(30, stats.Success);
        Assert.Equal(4, stats.Failed);
        Assert.Equal(5, stats.AwaitingRetry);
        Assert.Equal(6, stats.Exhausted);
        Assert.Equal(7, stats.Dlq);
        Assert.Equal(55, stats.Total);
#pragma warning disable CS0618
        Assert.Equal(55, stats.TotalMessages);
        Assert.Equal(30, stats.DeliveredMessages);
        Assert.Equal(30d / 55, stats.SuccessRate, 6);
#pragma warning restore CS0618
    }

    [Theory]
    [InlineData("csv", "text/csv", "id,eventType\nevt_1,\"order.created\"")]
    [InlineData("json", "application/json", "{\n  \"exportedAt\": \"x\",\n  \"count\": 0,\n  \"events\": []\n}")]
    public async Task Export_ReturnsTheRawBody(string format, string contentType, string body)
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.OK, body, contentType);

        var result = await new MessagesResource(client).ExportAsync("org_1", format: format, type: "events");

        Assert.Equal(body, result);
        Assert.Equal($"/api/organizations/org_1/outbound-messages/export?format={format}&type=events&limit=10000",
            handler.Requests[0].PathAndQuery);
    }

    [Fact]
    public async Task Replay_MapsTheNewMessageId()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.Created, """{ "data": { "originalMessageId": "msg_1", "newMessageId": "msg_2", "status": "queued" } }""");

        var result = await new MessagesResource(client).ReplayAsync("org_1", "msg_1");

        Assert.Equal("/api/organizations/org_1/outbound-messages/msg_1/replay", handler.Requests[0].PathAndQuery);
        Assert.Equal("msg_2", result.NewMessageId);
    }

    // ---- DLQ -----------------------------------------------------------------------------------

    [Fact]
    public async Task DlqList_MapsTheDlqFields()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.OK, """
        { "data": [{ "id": "msg_1", "eventId": "evt_1", "status": "dlq", "attempts": 5, "maxAttempts": 5,
                     "lastErrorType": "timeout", "lastErrorMessage": "timed out", "dlqReason": "max_retries_exceeded",
                     "dlqMovedAt": "2026-10-02T00:00:00.000Z", "dlqRetryCount": 1, "dlqLastRetryAt": "2026-10-03T00:00:00.000Z" }],
          "pagination": { "hasMore": false, "nextCursor": null } }
        """);

        var page = await new DlqResource(client).ListAsync(limit: 10, cursor: "opaque cursor");

        Assert.Equal("/api/outbound-messages/dlq/messages?limit=10&cursor=opaque%20cursor", handler.Requests[0].PathAndQuery);
        var message = Assert.Single(page.Data);
        Assert.Equal("max_retries_exceeded", message.DlqReason);
        Assert.Equal("2026-10-02T00:00:00.000Z", message.DlqMovedAt);
        Assert.Equal(1, message.DlqRetryCount);
        Assert.Equal("2026-10-03T00:00:00.000Z", message.DlqLastRetryAt);
        Assert.Equal("timeout", message.LastErrorType);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task DlqStats_UnwrapsDataAndMapsTheBreakdowns()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.OK, """
        { "data": { "total": 3, "byReason": { "max_retries_exceeded": 2, "unknown": 1 },
                    "byEndpoint": [{ "endpointId": "ep_1", "endpointUrl": "https://a.example", "count": 3 }],
                    "byEventType": [{ "eventType": "order.created", "count": 3 }] } }
        """);

        var stats = await new DlqResource(client).GetStatsAsync();

        Assert.Equal("/api/outbound-messages/dlq/stats", handler.Requests[0].PathAndQuery);
        Assert.Equal(3, stats.Total);
        Assert.Equal(2, stats.ByReason!["max_retries_exceeded"]);
        var endpoint = Assert.Single(stats.ByEndpoint!);
        Assert.Equal("ep_1", endpoint.EndpointId);
        Assert.Equal("https://a.example", endpoint.EndpointUrl);
        Assert.Equal(3, endpoint.Count);
        Assert.Equal("order.created", Assert.Single(stats.ByEventType!).EventType);
#pragma warning disable CS0618
        Assert.Equal(3, stats.TotalMessages);
#pragma warning restore CS0618
    }

    [Fact]
    public async Task DlqRetry_ReturnsTheNewMessageId()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.Created, """{ "data": { "originalMessageId": "msg_1", "newMessageId": "msg_2", "status": "queued" } }""");

        var result = await new DlqResource(client).RetryAsync("msg_1");

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/api/outbound-messages/dlq/msg_1/retry", handler.Requests[0].PathAndQuery);
        Assert.Equal("msg_1", result.OriginalMessageId);
        Assert.Equal("msg_2", result.NewMessageId);
        Assert.Equal("queued", result.Status);
    }

    // ---- endpoints, event types ----------------------------------------------------------------

    [Fact]
    public async Task EndpointStats_ReadsTheEndpointInsteadOfAMissingStatsRoute()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.OK, """
        { "data": { "id": "ep_1", "totalMessages": 10, "totalSuccesses": 8, "totalFailures": 2,
                    "avgResponseTimeMs": 120.5, "circuitFailureCount": 1 } }
        """);

        var stats = await new EndpointsResource(client).GetStatsAsync("ep_1");

        Assert.Equal("/api/webhook-endpoints/ep_1", handler.Requests[0].PathAndQuery);
        Assert.Equal(10, stats.TotalMessages);
        Assert.Equal(8, stats.TotalSuccesses);
        Assert.Equal(2, stats.TotalFailures);
        Assert.Equal(0.8, stats.SuccessRate, 6);
        Assert.Equal(120.5, stats.AverageLatency);
        Assert.Equal(1, stats.RecentFailures);
    }

    [Fact]
    public async Task RotateSecret_MapsTheTopLevelResponse()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.OK, """{ "secret": "whsec_abc", "previousSecretExpiresAt": "2026-10-17T01:00:00.000Z", "secretVersion": 3 }""");

        var result = await new EndpointsResource(client).RotateSecretAsync("ep_1");

        Assert.Equal("/api/webhook-endpoints/ep_1/rotate-secret", handler.Requests[0].PathAndQuery);
        Assert.Equal("whsec_abc", result.Secret);
        Assert.Equal("2026-10-17T01:00:00.000Z", result.PreviousSecretExpiresAt);
        Assert.Equal(3, result.SecretVersion);
    }

    [Fact]
    public async Task RotateSecret_MapsTheEd25519KeyPairResponse()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.OK, """{ "signatureScheme": "ed25519", "publicKey": "whpk_new", "previousPublicKey": "whpk_old", "previousSecretExpiresAt": "2026-10-17T01:00:00.000Z", "secretVersion": 4 }""");

        var result = await new EndpointsResource(client).RotateSecretAsync("ep_1");

        Assert.Equal("ed25519", result.SignatureScheme);
        Assert.Null(result.Secret);
        Assert.Equal("whpk_new", result.PublicKey);
        Assert.Equal("whpk_old", result.PreviousPublicKey);
        Assert.Equal("2026-10-17T01:00:00.000Z", result.PreviousSecretExpiresAt);
        Assert.Equal(4, result.SecretVersion);
    }

    [Fact]
    public void Endpoint_MapsSignatureSchemeAndPublicKey()
    {
        var endpoint = JsonSerializer.Deserialize<Endpoint>(
            """{ "id": "ep_1", "signatureScheme": "ed25519", "publicKey": "whpk_abc", "secretPrefix": "whsk_...", "hasSecret": true }""",
            HookbaseJson.Options)!;

        Assert.Equal("ed25519", endpoint.SignatureScheme);
        Assert.Equal("whpk_abc", endpoint.PublicKey);
    }

    [Fact]
    public void EndpointRequests_SendSignatureSchemeOnlyWhenSet()
    {
        var create = JsonSerializer.Serialize(
            new CreateEndpointRequest { ApplicationId = "app_1", Url = "https://x.example", SignatureScheme = "ed25519" }, HookbaseJson.Options);
        var update = JsonSerializer.Serialize(new UpdateEndpointRequest { SignatureScheme = "hmac-sha256" }, HookbaseJson.Options);
        var unset = JsonSerializer.Serialize(new UpdateEndpointRequest { Url = "https://x.example" }, HookbaseJson.Options);

        Assert.Contains("\"signatureScheme\":\"ed25519\"", create);
        Assert.Equal("{\"signatureScheme\":\"hmac-sha256\"}", update);
        Assert.DoesNotContain("signatureScheme", unset);
    }

    [Theory]
    [InlineData("{\"order_id\":\"o1\",\"total\":5}", "{\"order_id\":\"o1\",\"total\":5}")]
    [InlineData("[1,2]", "[1,2]")]
    [InlineData("\"already a string\"", "already a string")]
    [InlineData("null", null)]
    public void EventType_ExamplePayloadAcceptsAnyJsonValue(string json, string? expected)
    {
        var eventType = JsonSerializer.Deserialize<EventType>(
            "{\"id\":\"et_1\",\"name\":\"order.created\",\"examplePayload\":" + json + "}", HookbaseJson.Options)!;

        Assert.Equal(expected, eventType.ExamplePayload);
    }

    [Fact]
    public async Task ApplicationsListAll_FollowsTheCursorPastPageOne()
    {
        var (client, handler) = Create();
        handler.Respond(HttpStatusCode.OK, """{ "data": [{ "id": "app_1", "name": "A", "organizationId": "org_1" }], "pagination": { "hasMore": true, "nextCursor": "c/1" } }""")
               .Respond(HttpStatusCode.OK, """{ "data": [{ "id": "app_2", "name": "B", "organizationId": "org_1" }], "pagination": { "hasMore": false, "nextCursor": null } }""");

        var ids = new List<string>();
        await foreach (var app in new ApplicationsResource(client).ListAllAsync())
        {
            ids.Add(app.Id);
        }

        Assert.Equal(new[] { "app_1", "app_2" }, ids);
        Assert.Equal("/api/webhook-applications?limit=100&cursor=c%2F1", handler.Requests[1].PathAndQuery);
    }
}

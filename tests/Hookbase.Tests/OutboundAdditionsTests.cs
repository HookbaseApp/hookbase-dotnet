using Hookbase.Models.Messages;
using Hookbase.Models.Subscriptions;
using Hookbase.Resources;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Org-less message routes (org inferred from the API key, api/src/middleware/orgAccess.ts),
/// message attempts, DLQ filters and bulk operations, event-type and endpoint list filters,
/// endpoint secret grace period and the subscription fields, against outbound-messages.ts,
/// send-event.ts, event-types.ts, webhook-endpoints.ts and webhook-subscriptions.ts.
/// </summary>
public class OutboundAdditionsTests
{
    private const string SendResponse = """{"data":{"eventId":"evt_1","messagesQueued":1,"endpoints":[],"eventVersion":"v2"}}""";
    private const string EmptyPage = """{"data":[],"pagination":{"hasMore":false,"nextCursor":null}}""";

    [Fact]
    public async Task Send_without_an_org_id_posts_to_the_unscoped_route_with_eventVersion()
    {
        var (client, t) = TestTransport.Create();
        t.Respond(SendResponse, System.Net.HttpStatusCode.Created);

        var result = await client.Messages.SendAsync(new SendMessageRequest
        {
            EventType = "order.created",
            Payload = new Dictionary<string, object> { ["id"] = 1 },
            EventVersion = "v2"
        });

        Assert.Equal("POST /api/send-event", $"{t.Last.Method} {t.Last.Path}");
        Assert.Equal("v2", t.Last.Json.GetProperty("eventVersion").GetString());
        Assert.Equal("v2", result.EventVersion);
        Assert.Equal("evt_1", result.EventId);
    }

    [Fact]
    public async Task Send_with_an_org_id_keeps_the_scoped_route()
    {
        var (client, t) = TestTransport.Create();
        t.Respond(SendResponse);
        await client.Messages.SendAsync("org 1", new SendMessageRequest { EventType = "a", Payload = new() });
        Assert.Equal("/api/organizations/org%201/send-event", t.Last.Path);
        Assert.DoesNotContain("eventVersion", t.Last.Body);
    }

    [Fact]
    public async Task The_other_org_less_message_methods_use_the_unscoped_routes()
    {
        var (client, t) = TestTransport.Create();
        t.Respond(EmptyPage)
         .Respond("""{"data":{"id":"msg_1","status":"success","attempts":1,"maxAttempts":5,"traceId":"abc","eventVersion":"v1","deliveredEventVersion":"v1"}}""")
         .Respond("""{"data":{"originalMessageId":"msg_1","newMessageId":"msg_2","status":"pending"}}""")
         .Respond("""{"data":{"total":1,"pending":0,"processing":0,"success":1,"failed":0,"exhausted":0,"dlq":0,"awaitingRetry":0}}""")
         .Respond("id,status\n");

        await client.Messages.ListAsync(limit: 10, status: "failed");
        var message = await client.Messages.GetAsync("msg_1");
        await client.Messages.ReplayAsync("msg_1");
        await client.Messages.GetStatsSummaryAsync();
        var csv = await client.Messages.ExportAsync(new MessageExportOptions { Format = "csv", Type = "messages", Limit = 5 });

        Assert.Equal(new[]
        {
            "GET /api/outbound-messages",
            "GET /api/outbound-messages/msg_1",
            "POST /api/outbound-messages/msg_1/replay",
            "GET /api/outbound-messages/stats/summary",
            "GET /api/outbound-messages/export",
        }, t.Requests.Select(r => $"{r.Method} {r.Path}"));
        Assert.Equal("?limit=10&status=failed", t.Requests[0].Query);
        Assert.Equal("?format=csv&type=messages&limit=5", t.Requests[4].Query);
        Assert.Equal("abc", message.TraceId);
        Assert.Equal("v1", message.DeliveredEventVersion);
        Assert.Equal("id,status\n", csv);
    }

    [Fact]
    public async Task ListAttempts_maps_the_attempt_rows()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"data":[{"id":"att_2","messageId":"msg_1","attemptNumber":2,"status":"failed","responseStatus":503,
              "responseHeaders":"{\"retry-after\":\"5\"}","responseBody":"busy","responseTimeMs":120,"totalTimeMs":130,
              "errorType":"http_error","errorMessage":"503","errorCode":null,"requestUrl":"https://a.example/hook",
              "requestHeaders":{"content-type":"application/json"},"requestBodySize":42,"triggeredBy":"system",
              "triggeredByUser":null,"createdAt":"2026-10-20T10:00:00.000Z","completedAt":"2026-10-20T10:00:00.130Z","eventVersion":null}]}
            """);

        var attempts = await client.Messages.ListAttemptsAsync("msg_1");

        Assert.Equal("GET /api/outbound-messages/msg_1/attempts", $"{t.Last.Method} {t.Last.Path}");
        var a = Assert.Single(attempts);
        Assert.Equal(2, a.AttemptNumber);
        Assert.Equal(503, a.ResponseStatus);
        Assert.Equal(System.Text.Json.JsonValueKind.String, a.ResponseHeaders!.Value.ValueKind);
        Assert.Equal("application/json", a.RequestHeaders!.Value.GetProperty("content-type").GetString());
        Assert.Equal(42, a.RequestBodySize);
        Assert.Equal("system", a.TriggeredBy);
    }

    [Fact]
    public async Task Dlq_list_options_send_every_filter_the_API_reads()
    {
        var (client, t) = TestTransport.Create();
        t.Respond(EmptyPage);

        await client.Dlq.ListAsync(new DlqListOptions
        {
            Limit = 20, Cursor = "c1", ApplicationId = "app_1", EndpointId = "ep_1", EventType = "a.b",
            Reason = "max_retries_exceeded", StartDate = "2026-10-01", EndDate = "2026-10-02"
        });

        Assert.Equal("/api/outbound-messages/dlq/messages", t.Last.Path);
        Assert.Equal("?limit=20&cursor=c1&applicationId=app_1&endpointId=ep_1&eventType=a.b&reason=max_retries_exceeded&startDate=2026-10-01&endDate=2026-10-02", t.Last.Query);
    }

    [Fact]
    public async Task Dlq_bulk_retry_and_delete()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"data":{"queued":1,"skipped":1,"messages":[{"originalMessageId":"m1","newMessageId":"m9"}]}}""", System.Net.HttpStatusCode.Created)
         .Respond("""{"data":{"deleted":2}}""");

        var retried = await client.Dlq.RetryBulkAsync(new[] { "m1", "m2" });
        var deleted = await client.Dlq.DeleteBulkAsync(new[] { "m3", "m4" });

        Assert.Equal("POST /api/outbound-messages/dlq/retry-bulk", $"{t.Requests[0].Method} {t.Requests[0].Path}");
        Assert.Equal("""{"messageIds":["m1","m2"]}""", t.Requests[0].Body);
        Assert.Equal(1, retried.Queued);
        Assert.Equal(1, retried.Skipped);
        Assert.Equal("m9", retried.Messages!.Single().NewMessageId);

        Assert.Equal("DELETE /api/outbound-messages/dlq/bulk", $"{t.Requests[1].Method} {t.Requests[1].Path}");
        Assert.Equal("""{"messageIds":["m3","m4"]}""", t.Requests[1].Body);
        Assert.Equal(2, deleted);
    }

    [Fact]
    public async Task EventTypes_list_sends_the_API_filters_and_no_longer_the_ignored_applicationId()
    {
        var (client, t) = TestTransport.Create();
        t.Respond(EmptyPage).Respond(EmptyPage);

        await client.EventTypes.ListAsync(limit: 5, applicationId: "app_1");
        await client.EventTypes.ListAsync(new EventTypeListOptions { Limit = 10, Category = "orders", IsEnabled = false, Search = "ord" });

        Assert.Equal("?limit=5", t.Requests[0].Query);
        Assert.Equal("?limit=10&category=orders&isEnabled=false&search=ord", t.Requests[1].Query);
    }

    [Fact]
    public async Task Endpoints_list_options_and_rotate_grace_period()
    {
        var (client, t) = TestTransport.Create();
        t.Respond(EmptyPage)
         .Respond("""{"secret":"whsec_new","previousSecretExpiresAt":"2026-10-20T11:00:00Z","message":"ok"}""")
         .Respond("""{"secret":"whsec_new2","message":"ok"}""");

        await client.Endpoints.ListAsync(new EndpointListOptions { ApplicationId = "app_1", IsDisabled = true, CircuitState = "open" });
        await client.Endpoints.RotateSecretAsync("ep_1", 0);
        await client.Endpoints.RotateSecretAsync("ep_1");

        Assert.Equal("?limit=50&applicationId=app_1&isDisabled=true&circuitState=open", t.Requests[0].Query);
        Assert.Equal("POST /api/webhook-endpoints/ep_1/rotate-secret", $"{t.Requests[1].Method} {t.Requests[1].Path}");
        Assert.Equal("""{"gracePeriodSeconds":0}""", t.Requests[1].Body);
        Assert.Null(t.Requests[2].Body);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Endpoints.RotateSecretAsync("ep_1", 86401));
    }

    [Fact]
    public async Task Subscription_create_and_update_send_the_filter_fields()
    {
        var (client, t) = TestTransport.Create();
        const string row = """{"data":{"id":"sub_1","endpointId":"ep_1","eventTypeId":"et_1","isEnabled":true,"createdAt":"2026-10-20T10:00:00Z"}}""";
        t.Respond(row, System.Net.HttpStatusCode.Created).Respond(row);

        await client.Subscriptions.CreateAsync(new CreateSubscriptionRequest
        {
            EndpointId = "ep_1",
            EventTypeId = "et_1",
            FilterExpression = "amount = 10",
            LabelFilters = new Dictionary<string, object> { ["region"] = new[] { "us", "eu" }, ["tier"] = "gold" },
            LabelFilterMode = "any",
            TransformId = "3f1c2a3e-1111-4222-8333-444455556666",
            PriorityOverride = 1,
            EventVersion = "latest"
        });
        await client.Subscriptions.UpdateAsync("sub_1", new UpdateSubscriptionRequest { IsEnabled = false, FilterExpression = "x" });

        Assert.Equal("""{"endpointId":"ep_1","eventTypeId":"et_1","filterExpression":"amount = 10","labelFilters":{"region":["us","eu"],"tier":"gold"},"labelFilterMode":"any","transformId":"3f1c2a3e-1111-4222-8333-444455556666","priorityOverride":1,"eventVersion":"latest"}""", t.Requests[0].Body);
        Assert.Equal("""{"isEnabled":false,"filterExpression":"x"}""", t.Requests[1].Body);
    }

    [Fact]
    public async Task Subscription_get_maps_the_nested_event_type_and_version_fields()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"data":{"id":"sub_1","endpointId":"ep_1","eventTypeId":"et_1","filterExpression":null,
              "labelFilters":{"region":["us"]},"labelFilterMode":"all","transformId":null,"priorityOverride":null,
              "isEnabled":true,"createdAt":"2026-10-20T10:00:00Z","eventVersion":"latest","eventVersionDeprecated":false,
              "eventVersionSunsetAt":null,"eventType":{"id":"et_1","name":"order.created","displayName":"Order created"},
              "endpoint":{"id":"ep_1","url":"https://a.example"},"application":{"id":"app_1","name":"Acme"}}}
            """);

        var sub = await client.Subscriptions.GetAsync("sub_1");

        Assert.Equal("order.created", sub.EventType!.Name);
        Assert.Equal("latest", sub.EventVersion);
        Assert.Equal("us", sub.LabelFilters!.Value.GetProperty("region")[0].GetString());
        Assert.Equal("https://a.example", sub.Endpoint!.Value.GetProperty("url").GetString());
    }
}

using System.Text.Json;
using Hookbase.Json;
using Hookbase.Models.Common;
using Hookbase.Models.Endpoints;
using Hookbase.Models.Routes;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Holds while a circuit is open (api migrations 0134/0135, utils/holds.ts): the held and
/// awaiting_retry statuses, holdExpiresAt, holdOnCircuitOpen and the held counts.
/// </summary>
public class HoldsTests
{
    [Theory]
    [InlineData(MessageStatus.Pending, "pending")]
    [InlineData(MessageStatus.Processing, "processing")]
    [InlineData(MessageStatus.Success, "success")]
    [InlineData(MessageStatus.Failed, "failed")]
    [InlineData(MessageStatus.AwaitingRetry, "awaiting_retry")]
    [InlineData(MessageStatus.AwaitingPull, "awaiting_pull")]
    [InlineData(MessageStatus.FifoWaiting, "fifo_waiting")]
    [InlineData(MessageStatus.Held, "held")]
    [InlineData(MessageStatus.Exhausted, "exhausted")]
    [InlineData(MessageStatus.Dlq, "dlq")]
    public async Task Message_status_filters_use_the_API_names(MessageStatus status, string wire)
    {
        Assert.Equal(wire, status.ToWireName());

        var (client, t) = TestTransport.Create();
        t.Respond("""{"data":[],"pagination":{"hasMore":false,"nextCursor":null}}""")
         .Respond("id,status\n");
        await client.Messages.ListAsync(status: status.ToWireName());
        Assert.Equal("GET /api/outbound-messages", $"{t.Last.Method} {t.Last.Path}");
        Assert.Contains($"status={wire}", t.Last.Query);

        await client.Messages.ExportAsync(new Hookbase.Models.Messages.MessageExportOptions { Status = status.ToWireName() });
        Assert.Contains($"status={wire}", t.Last.Query);
    }

    [Fact]
    public void MessageStatus_has_every_outbound_status_and_keeps_the_old_values()
    {
        Assert.Equal(10, Enum.GetValues<MessageStatus>().Length);
        // The four 5.1 members keep their numeric values.
        Assert.Equal(0, (int)MessageStatus.Pending);
        Assert.Equal(1, (int)MessageStatus.Success);
        Assert.Equal(2, (int)MessageStatus.Failed);
        Assert.Equal(3, (int)MessageStatus.Exhausted);
        Assert.Equal("awaiting_retry", DeliveryStatus.AwaitingRetry.ToWireName());
    }

    [Fact]
    public async Task Held_deliveries_and_messages_read_holdExpiresAt()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"deliveries":[
              {"id":"d1","status":"held","nextRetryAt":null,"holdExpiresAt":"2026-11-12T10:00:00.000Z","attemptCount":2},
              {"id":"d2","status":"awaiting_retry","nextRetryAt":"2026-11-11T10:05:00.000Z","holdExpiresAt":null}],
             "limit":20,"offset":0}
            """)
         .Respond("""
            {"data":[{"id":"m1","status":"held","attempts":1,"maxAttempts":5,"holdExpiresAt":"2026-11-14T00:00:00.000Z"}],
             "pagination":{"hasMore":false,"nextCursor":null}}
            """);

        var deliveries = await client.Deliveries.ListAsync(status: DeliveryStatus.Held);
        Assert.Contains("status=held", t.Last.Query);
        Assert.Equal("held", deliveries.Data[0].Status);
        Assert.Null(deliveries.Data[0].NextRetryAt);
        Assert.Equal("2026-11-12T10:00:00.000Z", deliveries.Data[0].HoldExpiresAt);
        Assert.Null(deliveries.Data[1].HoldExpiresAt);

        var messages = await client.Messages.ListAsync(status: MessageStatus.Held.ToWireName());
        Assert.Equal("2026-11-14T00:00:00.000Z", messages.Data[0].HoldExpiresAt);
    }

    [Fact]
    public async Task Send_reads_messagesHeld_and_stats_read_held()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"data":{"eventId":"evt_1","messagesQueued":3,"messagesHeld":2,"endpoints":[]}}""")
         .Respond("""{"data":{"eventId":"evt_2","messagesQueued":1,"endpoints":[]}}""")
         .Respond("""{"data":{"pending":1,"processing":0,"success":5,"failed":0,"awaitingRetry":2,"held":4,"exhausted":0,"dlq":1,"total":13}}""");

        var held = await client.Messages.SendAsync(new Hookbase.Models.Messages.SendMessageRequest { EventType = "order.created", Payload = new Dictionary<string, object> { ["id"] = 1 } });
        Assert.Equal(3, held.MessagesQueued);
        Assert.Equal(2, held.MessagesHeld);

        var none = await client.Messages.SendAsync(new Hookbase.Models.Messages.SendMessageRequest { EventType = "order.created", Payload = new Dictionary<string, object> { ["id"] = 2 } });
        Assert.Equal(0, none.MessagesHeld);

        var stats = await client.Messages.GetStatsSummaryAsync();
        Assert.Equal(4, stats.Held);
        Assert.Equal(2, stats.AwaitingRetry);
        Assert.Equal(13, stats.Total);
    }

    [Fact]
    public async Task Routes_read_and_send_holdOnCircuitOpen_and_read_the_held_count()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"route":{"id":"r1","name":"R","holdOnCircuitOpen":false,"heldCount":10000,"heldCountCapped":true,"isActive":true}}
            """)
         .Respond("""{"route":{"id":"r2","name":"R2","holdOnCircuitOpen":true}}""")
         .Respond("""{"success":true}""")
         .Respond("""{"success":true}""")
         .Respond("""{"routes":[{"id":"r3","name":"Old"}],"pagination":{"total":1,"page":1,"pageSize":20}}""");

        var route = await client.Routes.GetAsync("r1");
        Assert.False(route.HoldOnCircuitOpen);
        Assert.Equal(10000, route.HeldCount);
        Assert.True(route.HeldCountCapped);

        await client.Routes.CreateAsync(new CreateRouteRequest { Name = "R2", SourceId = "s", DestinationId = "d", HoldOnCircuitOpen = false });
        Assert.False(t.Last.Json.GetProperty("holdOnCircuitOpen").GetBoolean());

        await client.Routes.UpdateAsync("r2", new UpdateRouteRequest { HoldOnCircuitOpen = true });
        Assert.Equal("""{"holdOnCircuitOpen":true}""", t.Last.Body);

        // Null is not sent: the API answers 400 to an explicit null on create.
        await client.Routes.UpdateAsync("r2", new UpdateRouteRequest { Name = "Renamed" });
        Assert.False(t.Last.Json.TryGetProperty("holdOnCircuitOpen", out _));

        // A row without the field (an API before 0134) reads as the default, on.
        var page = await client.Routes.ListAsync();
        Assert.True(page.Data[0].HoldOnCircuitOpen);
        Assert.Null(page.Data[0].HeldCount);
    }

    [Fact]
    public void A_route_create_without_HoldOnCircuitOpen_does_not_send_it()
    {
        var body = JsonSerializer.SerializeToElement(
            new CreateRouteRequest { Name = "R", SourceId = "s", DestinationId = "d" }, HookbaseJson.Options);
        Assert.False(body.TryGetProperty("holdOnCircuitOpen", out _));

        var export = JsonSerializer.SerializeToElement(new ExportedRoute { Name = "R" }, HookbaseJson.Options);
        Assert.False(export.TryGetProperty("holdOnCircuitOpen", out _));
        var exportOff = JsonSerializer.SerializeToElement(new ExportedRoute { Name = "R", HoldOnCircuitOpen = false }, HookbaseJson.Options);
        Assert.False(exportOff.GetProperty("holdOnCircuitOpen").GetBoolean());
    }

    [Fact]
    public async Task Endpoints_read_and_send_holdOnCircuitOpen_and_read_the_held_count()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"data":{"id":"ep_1","url":"https://a.example","holdOnCircuitOpen":false,"heldCount":3,"heldCountCapped":false}}""")
         .Respond("""{"data":{"id":"ep_2","url":"https://b.example","holdOnCircuitOpen":true,"secret":"whsec_x"}}""")
         .Respond("""{"data":{"id":"ep_2","url":"https://b.example","holdOnCircuitOpen":false}}""")
         .Respond("""{"data":{"id":"ep_2","url":"https://c.example","holdOnCircuitOpen":false}}""");

        var endpoint = await client.Endpoints.GetAsync("ep_1");
        Assert.False(endpoint.HoldOnCircuitOpen);
        Assert.Equal(3, endpoint.HeldCount);
        Assert.False(endpoint.HeldCountCapped);

        await client.Endpoints.CreateAsync(new CreateEndpointRequest { ApplicationId = "app_1", Url = "https://b.example", HoldOnCircuitOpen = true });
        Assert.True(t.Last.Json.GetProperty("holdOnCircuitOpen").GetBoolean());

        var updated = await client.Endpoints.UpdateAsync("ep_2", new UpdateEndpointRequest { HoldOnCircuitOpen = false });
        Assert.Equal("""{"holdOnCircuitOpen":false}""", t.Last.Body);
        Assert.False(updated.HoldOnCircuitOpen);

        await client.Endpoints.UpdateAsync("ep_2", new UpdateEndpointRequest { Url = "https://c.example" });
        Assert.False(t.Last.Json.TryGetProperty("holdOnCircuitOpen", out _));
    }

    [Fact]
    public async Task Debug_deliveries_read_typed()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"event":{"id":"evt_1"},"request":{"headers":{}},"payload":{"a":1},"transient":false,
             "deliveries":[
               {"id":"d1","status":"held","statusCode":503,"durationMs":120,"attempt":2,"maxAttempts":5,
                "nextRetryAt":null,"holdExpiresAt":"2026-11-12T10:00:00.000Z","error":"HTTP 503",
                "responseBody":"down","responseHeaders":{"retry-after":"30"},"transformedPayload":{"b":2},
                "routeId":"r1","destinationId":"dst_1","destination":{"name":"Orders","url":"https://a.example","method":"POST"},
                "startedAt":"2026-11-11T10:00:00.000Z","completedAt":"2026-11-11T10:00:01.000Z","failoverIndex":0,
                "parentDeliveryId":null,"fingerprint":"fp1","retryAfter":"30","clusterCount":4,
                "patternHints":[{"id":"upstream-unavailable","category":"upstream","title":"Down","hypothesis":"h","suggestedFix":"f"}],
                "routeCircuit":{"state":"open","openedAt":"2026-11-11T09:59:00.000Z","cooldownSeconds":300,"failureThreshold":5,"consecutiveFailures":5}},
               {"id":"d2","status":"delivered","failoverIndex":1,"patternHints":[],"routeCircuit":null,"clusterCount":null}],
             "timing":{},"filterEvaluations":[],"recentConfigChanges":{"sinceDays":7,"entries":[]},"debug":{"curlCommand":null}}
            """)
         .Respond("""{"event":{"id":"evt_2"},"payload":null}""");

        var info = await client.Events.DebugAsync("evt_1");
        Assert.NotNull(info.Deliveries);
        var deliveries = info.GetDeliveries();
        Assert.Equal(2, deliveries.Count);

        var held = deliveries[0];
        Assert.Equal("held", held.Status);
        Assert.Equal(503, held.StatusCode);
        Assert.Equal(2, held.Attempt);
        Assert.Equal("2026-11-12T10:00:00.000Z", held.HoldExpiresAt);
        Assert.Null(held.NextRetryAt);
        Assert.Equal("30", held.ResponseHeaders!["retry-after"]);
        Assert.Equal(2, held.TransformedPayload!.Value.GetProperty("b").GetInt32());
        Assert.Equal("Orders", held.Destination!.Name);
        Assert.Equal(4, held.ClusterCount);
        Assert.Equal("upstream", held.PatternHints![0].Category);
        Assert.Equal("open", held.RouteCircuit!.State);
        Assert.Equal(5, held.RouteCircuit.ConsecutiveFailures);

        Assert.Equal(1, deliveries[1].FailoverIndex);
        Assert.Null(deliveries[1].RouteCircuit);
        Assert.Empty(deliveries[1].PatternHints!);

        var none = await client.Events.DebugAsync("evt_2");
        Assert.Empty(none.GetDeliveries());
    }
}

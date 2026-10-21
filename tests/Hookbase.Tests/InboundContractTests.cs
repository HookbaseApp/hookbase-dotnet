using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hookbase.Models.CronJobs;
using Hookbase.Models.Destinations;
using Hookbase.Models.Routes;
using Hookbase.Models.Sources;
using Hookbase.Models.Transforms;
using Hookbase.Models.Tunnels;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Requests and response mapping for the inbound surface (events, deliveries, transforms, cron,
/// tunnels, route circuit breaker, imports), checked against the response shapes in
/// api/src/routes/events.ts, deliveries.ts, transforms.ts, cron.ts, tunnels.ts, routes.ts,
/// sources.ts and destinations.ts. No request leaves the process: a fake handler records each one
/// and returns the next canned response queued for its "METHOD path".
/// </summary>
public class InboundContractTests
{
    private sealed record Recorded(string Route, string Query, string? Body);

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Queue<string>> _responses = new();
        public List<Recorded> Requests { get; } = new();

        public QueueHandler Respond(string route, string body)
        {
            if (!_responses.TryGetValue(route, out var queue)) _responses[route] = queue = new Queue<string>();
            queue.Enqueue(body);
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var route = $"{request.Method} {request.RequestUri!.AbsolutePath}";
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new Recorded(route, Uri.UnescapeDataString(request.RequestUri.Query), body));
            if (!_responses.TryGetValue(route, out var queue) || queue.Count == 0)
                throw new InvalidOperationException("No response queued for " + route);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(queue.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }

    private static (HookbaseClient Client, QueueHandler Handler) Create()
    {
        var handler = new QueueHandler();
        var client = new HookbaseClient(new HookbaseClientOptions
        {
            ApiKey = "whr_test",
            BaseUrl = "https://api.test",
            MaxRetries = 0,
            HttpClient = new HttpClient(handler)
        });
        return (client, handler);
    }

    private static JsonObject Sent(Recorded request) => JsonNode.Parse(request.Body!)!.AsObject();

    private static string EventRow(string id) => $$"""
        {"id":"{{id}}","organizationId":"org_1","sourceId":"src_1","eventType":"order.created",
         "payloadHash":"h","payloadKey":"inline:{{id}}","headers":"{\"content-type\":\"application/json\"}",
         "ipAddress":"203.0.113.9","signatureValid":true,"receivedAt":"2026-10-20T10:00:00.000Z",
         "sourceName":"Shop","sourceSlug":"shop","status":"delivered","method":"POST",
         "deliveryStats":{"total":1,"delivered":1,"failed":0,"pending":0} }
        """;

    // Copied from the deliveries list in GET /api/deliveries (deliveries.ts); responseHeaders is
    // stored as JSON text.
    private const string DeliveryRow = """
        {"id":"dlv_1","organizationId":"org_1","eventId":"evt_1","routeId":"rt_1","destinationId":"dst_1",
         "status":"failed","attemptCount":3,"maxAttempts":5,"nextRetryAt":null,"responseStatus":502,
         "responseKey":"responses/dlv_1","responseHeaders":"{\"content-type\":\"text/plain\"}",
         "responseBody":"bad gateway","latencyMs":812,"errorMessage":"HTTP 502","deliveredAt":null,
         "createdAt":"2026-10-20T10:00:01.000Z","updatedAt":"2026-10-20T10:05:00.000Z",
         "destinationName":"Orders API","eventType":"order.created"}
        """;

    // ---- events --------------------------------------------------------------------------------

    [Fact]
    public async Task EventsListAll_FollowsTheCursor_AndToleratesANullTotal()
    {
        var (client, handler) = Create();
        handler
            .Respond("GET /api/events", $$"""{"events":[{{EventRow("evt_1")}},{{EventRow("evt_2")}}],"total":null,"limit":100,"hasMore":true,"nextCursor":"c1"}""")
            .Respond("GET /api/events", $$"""{"events":[{{EventRow("evt_3")}}],"total":null,"limit":100,"hasMore":false,"nextCursor":null}""");

        var ids = new List<string?>();
        await foreach (var e in client.Events.ListAllAsync(sourceId: "shop")) ids.Add(e.Id);

        Assert.Equal(new[] { "evt_1", "evt_2", "evt_3" }, ids);
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain("cursor=", handler.Requests[0].Query);
        Assert.Contains("cursor=c1", handler.Requests[1].Query);
        Assert.All(handler.Requests, r => Assert.DoesNotContain("offset=", r.Query));
        Assert.All(handler.Requests, r => Assert.Contains("sourceId=shop", r.Query));
    }

    [Fact]
    public async Task EventsListPage_MapsCursorAndMethod()
    {
        var (client, handler) = Create();
        handler.Respond("GET /api/events", $$"""{"events":[{{EventRow("evt_1")}}],"total":null,"limit":1,"hasMore":true,"nextCursor":"abc="}""");

        var page = await client.Events.ListPageAsync(limit: 1);

        Assert.True(page.HasMore);
        Assert.Equal("abc=", page.NextCursor);
        Assert.Equal("POST", page.Data.Single().Method);
        Assert.Equal(1, page.Data.Single().DeliveryStats!.Delivered);
    }

#pragma warning disable CS0618 // the obsolete offset overload is still supported
    [Fact]
    public async Task EventsListWithOffset_WalksTheCursorInsteadOfSendingOffset()
    {
        var (client, handler) = Create();
        handler
            .Respond("GET /api/events", $$"""{"events":[{{EventRow("evt_1")}},{{EventRow("evt_2")}}],"total":null,"limit":2,"hasMore":true,"nextCursor":"c1"}""")
            .Respond("GET /api/events", $$"""{"events":[{{EventRow("evt_3")}},{{EventRow("evt_4")}}],"total":null,"limit":2,"hasMore":true,"nextCursor":"c2"}""");

        var page = await client.Events.ListAsync(limit: 2, offset: 2);

        Assert.Equal(new[] { "evt_3", "evt_4" }, page.Data.Select(e => e.Id));
        Assert.Equal(2, page.Page);
        Assert.True(page.HasMore);
        Assert.Contains("limit=2", handler.Requests[0].Query);
        Assert.Contains("cursor=c1", handler.Requests[1].Query);
        Assert.All(handler.Requests, r => Assert.DoesNotContain("offset=", r.Query));
    }

    [Fact]
    public async Task EventsListWithOffset_LastPageHasNoMore()
    {
        var (client, handler) = Create();
        handler.Respond("GET /api/events", $$"""{"events":[{{EventRow("evt_1")}}],"total":null,"limit":20,"hasMore":false,"nextCursor":null}""");

        var page = await client.Events.ListAsync();

        Assert.Single(page.Data);
        Assert.False(page.HasMore);
    }
#pragma warning restore CS0618

    [Fact]
    public async Task EventsGet_FoldsTopLevelPayloadAndDeliveriesIntoTheDetail()
    {
        var (client, handler) = Create();
        // Shape of GET /api/events/:eventId (events.ts): payload, transient and deliveries sit beside event.
        handler.Respond("GET /api/events/evt_1", $$"""
            {"event":{"id":"evt_1","organizationId":"org_1","sourceId":"src_1","eventType":"order.created",
                      "payloadHash":"h","payloadKey":"inline:evt_1","headers":"{\"x-a\":\"1\"}","ipAddress":"203.0.113.9",
                      "signatureValid":true,"receivedAt":"2026-10-20T10:00:00.000Z","sourceName":"Shop",
                      "encryptFields":null,"maskFields":null,"status":"failed","method":"POST"},
             "payload":{"order_id":"o_1","total":42},
             "transient":false,
             "deliveries":[{{DeliveryRow.Replace("\"eventType\":\"order.created\"", "\"destinationUrl\":\"https://orders.example/hook\"")}}]}
            """);

        var detail = await client.Events.GetAsync("evt_1");

        Assert.Equal("evt_1", detail.Id);
        Assert.Equal("failed", detail.Status);
        Assert.Equal("POST", detail.Method);
        Assert.False(detail.Transient);
        Assert.Equal("1", detail.Headers!["x-a"]);
        var payload = Assert.IsType<JsonElement>(detail.Payload);
        Assert.Equal("o_1", payload.GetProperty("order_id").GetString());

        var delivery = Assert.Single(detail.Deliveries!);
        Assert.Equal("dlv_1", delivery.Id);
        Assert.Equal(502, delivery.ResponseStatus);
        Assert.Equal(3, delivery.AttemptCount);
        Assert.Equal("Orders API", delivery.DestinationName);
        Assert.Equal("https://orders.example/hook", delivery.DestinationUrl);
    }

    [Fact]
    public async Task EventsGet_TransientEventHasNoPayloadAndEmptyDeliveries()
    {
        var (client, handler) = Create();
        handler.Respond("GET /api/events/evt_t", """
            {"event":{"id":"evt_t","status":"no_routes"},"payload":null,"transient":true,"deliveries":[]}
            """);

        var detail = await client.Events.GetAsync("evt_t");

        Assert.True(detail.Transient);
        Assert.Null(detail.Payload);
        Assert.Empty(detail.Deliveries!);
    }

    // ---- deliveries ----------------------------------------------------------------------------

    [Fact]
    public async Task DeliveriesList_MapsTheApiFieldNames()
    {
        var (client, handler) = Create();
        handler.Respond("GET /api/deliveries", $$"""{"deliveries":[{{DeliveryRow}}],"limit":20,"offset":0}""");

        var page = await client.Deliveries.ListAsync(eventId: "evt_1");
        var d = page.Data.Single();

        Assert.Equal("failed", d.Status);
        Assert.Equal(502, d.ResponseStatus);
        Assert.Equal(3, d.AttemptCount);
        Assert.Equal(5, d.MaxAttempts);
        Assert.Equal(812, d.LatencyMs);
        Assert.Equal("HTTP 502", d.ErrorMessage);
        Assert.Null(d.DeliveredAt);
        Assert.Equal("responses/dlv_1", d.ResponseKey);
        Assert.Equal("text/plain", d.ResponseHeaders!["content-type"]);
        Assert.Equal("2026-10-20T10:05:00.000Z", d.UpdatedAt);
        Assert.Equal("Orders API", d.DestinationName);
        Assert.Equal("order.created", d.EventType);
        Assert.Contains("eventId=evt_1", handler.Requests.Single().Query);

#pragma warning disable CS0618 // the old names stay as aliases
        Assert.Equal(502, d.StatusCode);
        Assert.Equal(3, d.Attempts);
        Assert.Equal("HTTP 502", d.Error);
        Assert.Equal(812, d.Duration);
#pragma warning restore CS0618
    }

    [Fact]
    public async Task DeliveriesList_PagesByTheLimitTheApiApplied()
    {
        var (client, handler) = Create();
        handler.Respond("GET /api/deliveries", """{"deliveries":[],"limit":100,"offset":200}""");

        var page = await client.Deliveries.ListAsync(limit: 500, offset: 200);

        Assert.Equal(100, page.PageSize);
        Assert.Equal(3, page.Page);
    }

    [Fact]
    public async Task DeliveriesGet_MapsDestinationUrl()
    {
        var (client, handler) = Create();
        handler.Respond("GET /api/deliveries/dlv_1", $$"""{"delivery":{{DeliveryRow.Replace("\"eventType\":\"order.created\"", "\"destinationUrl\":\"https://orders.example/hook\"")}}}""");

        var d = await client.Deliveries.GetAsync("dlv_1");

        Assert.Equal("https://orders.example/hook", d.DestinationUrl);
        Assert.Equal(502, d.ResponseStatus);
#pragma warning disable CS0618
        Assert.Equal("https://orders.example/hook", d.Destination!.Url);
        Assert.Equal("Orders API", d.Destination.Name);
#pragma warning restore CS0618
    }

    // ---- transforms ----------------------------------------------------------------------------

    [Fact]
    public async Task TransformTest_ReadsResultAndContentType()
    {
        var (client, handler) = Create();
        handler.Respond("POST /api/transforms/test", """{"success":true,"result":{"id":"o_1"},"contentType":"application/json"}""");

        var result = await client.Transforms.TestAsync(new TestTransformRequest
        {
            Code = "{ \"id\": order_id }",
            Payload = new Dictionary<string, object> { ["order_id"] = "o_1" }
        });

        Assert.True(result.Success);
        Assert.Equal("application/json", result.ContentType);
        Assert.Equal("o_1", Assert.IsType<JsonElement>(result.Result).GetProperty("id").GetString());
    }

    [Fact]
    public async Task TransformTest_FailureIsReportedNotThrown()
    {
        var (client, handler) = Create();
        handler.Respond("POST /api/transforms/test", """{"success":false,"error":"Unexpected token"}""");

        var result = await client.Transforms.TestAsync(new TestTransformRequest
        {
            Code = "{",
            Payload = new Dictionary<string, object>()
        });

        Assert.False(result.Success);
        Assert.Equal("Unexpected token", result.Error);
    }

    // ---- cron and tunnels ----------------------------------------------------------------------

    [Fact]
    public async Task CronCreate_SendsPayload()
    {
        var (client, handler) = Create();
        handler.Respond("POST /api/cron", """{"cronJob":{"id":"cron_1","name":"Ping","cronExpression":"*/5 * * * *","url":"https://a.example"}}""");

        await client.CronJobs.CreateAsync(new CreateCronJobRequest
        {
            Name = "Ping",
            Url = "https://a.example",
            CronExpression = "*/5 * * * *",
            Payload = "{\"ping\":true}"
        });

        var sent = Sent(handler.Requests.Single());
        Assert.Equal("{\"ping\":true}", sent["payload"]!.GetValue<string>());
        Assert.False(sent.ContainsKey("body"));
    }

    [Fact]
    public async Task TunnelCreate_SendsOnlyFieldsTheApiReads()
    {
        var (client, handler) = Create();
        handler.Respond("POST /api/tunnels", """{"tunnel":{"id":"tun_1","name":"dev","subdomain":"dev-1"},"tunnelUrl":"https://x/t/dev-1","wsUrl":"wss://x"}""");

        var tunnel = await client.Tunnels.CreateAsync(new CreateTunnelRequest { Name = "dev", Subdomain = "dev-1" });

        Assert.Equal("tun_1", tunnel.Id);
        var sent = Sent(handler.Requests.Single());
        Assert.Equal(new[] { "name", "subdomain" }, sent.Select(p => p.Key).OrderBy(k => k));
    }

    // ---- route circuit breaker -----------------------------------------------------------------

    [Fact]
    public async Task CircuitStatus_MapsTheApiFields()
    {
        var (client, handler) = Create();
        handler.Respond("GET /api/routes/rt_1/circuit-status", """
            {"circuitState":"open","circuitOpenedAt":"2026-10-20T10:00:00.000Z","cooldownSeconds":300,
             "probeAttempts":0,"probeSuccessThreshold":2,"failureThreshold":5,"consecutiveFailures":7,
             "timeUntilProbeSeconds":120}
            """);

        var status = await client.Routes.GetCircuitStatusAsync("rt_1");

        Assert.Equal("open", status.CircuitState);
        Assert.Equal(300, status.CooldownSeconds);
        Assert.Equal(5, status.FailureThreshold);
        Assert.Equal(7, status.ConsecutiveFailures);
        Assert.Equal(120, status.TimeUntilProbeSeconds);
    }

    [Fact]
    public async Task CircuitReset_MapsTheApiFields()
    {
        var (client, handler) = Create();
        handler.Respond("POST /api/routes/rt_1/reset-circuit", """{"success":true,"circuitState":"closed","previousState":"open"}""");

        var result = await client.Routes.ResetCircuitAsync("rt_1");

        Assert.True(result.Success);
        Assert.Equal("closed", result.CircuitState);
        Assert.Equal("open", result.PreviousState);
    }

    [Fact]
    public async Task UpdateCircuitConfig_AcceptsTheSuccessOnlyAnswer_AndReadsTheRouteBack()
    {
        var (client, handler) = Create();
        handler
            .Respond("PATCH /api/routes/rt_1/circuit-config", """{"success":true}""")
            .Respond("GET /api/routes/rt_1", """{"route":{"id":"rt_1","name":"r","circuitCooldownSeconds":60}}""");

        var route = await client.Routes.UpdateCircuitConfigAsync("rt_1", new UpdateCircuitConfigRequest { CircuitCooldownSeconds = 60 });

        Assert.Equal(60, route.CircuitCooldownSeconds);
        Assert.Equal(60, Sent(handler.Requests[0])["circuitCooldownSeconds"]!.GetValue<int>());
        Assert.Equal("GET /api/routes/rt_1", handler.Requests[1].Route);
    }

    // ---- imports -------------------------------------------------------------------------------

    [Fact]
    public async Task SourcesImport_SendsAConflictStrategy_AndMapsSummaryAndDetails()
    {
        var (client, handler) = Create();
        handler.Respond("POST /api/sources/import", """
            {"success":true,"summary":{"imported":1,"skipped":1,"overwritten":0,"failed":1},
             "details":{"imported":["Shop"],"skipped":["Old"],"overwritten":[],"failed":[{"name":"Bad","error":"Slug is required"}]}}
            """);

        var result = await client.Sources.ImportAsync(new List<CreateSourceRequest>
        {
            new() { Name = "Shop", Slug = "shop" }
        });

        var sent = Sent(handler.Requests.Single());
        Assert.Equal("skip", sent["conflictStrategy"]!.GetValue<string>());
        Assert.Equal("shop", sent["sources"]![0]!["slug"]!.GetValue<string>());

        Assert.True(result.Success);
        Assert.Equal(1, result.Summary!.Imported);
        Assert.Equal(1, result.Summary.Failed);
        Assert.Equal(new[] { "Shop" }, result.Details!.Imported);
        Assert.Equal("Slug is required", result.Details.Failed!.Single()["error"]);
#pragma warning disable CS0618
        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(new[] { "Bad: Slug is required" }, result.Errors);
#pragma warning restore CS0618
    }

    [Fact]
    public async Task SourcesImport_ValidateOnly_MapsValidationResults()
    {
        var (client, handler) = Create();
        handler.Respond("POST /api/sources/import", """
            {"valid":false,
             "validationResults":[{"index":0,"name":"Shop","slug":"shop","status":"rename","errors":[],"warnings":["exists"],"resolvedSlug":"shop-1"},
                                  {"index":1,"name":"","slug":"","status":"error","errors":["Name is required"],"warnings":[]}],
             "summary":{"total":2,"toCreate":1,"toOverwrite":0,"toSkip":0,"errors":1}}
            """);

        var result = await client.Sources.ImportAsync(
            new List<CreateSourceRequest> { new() { Name = "Shop", Slug = "shop" }, new() { Name = "", Slug = "" } },
            conflictStrategy: "rename",
            validateOnly: true);

        var sent = Sent(handler.Requests.Single());
        Assert.Equal("rename", sent["conflictStrategy"]!.GetValue<string>());
        Assert.True(sent["validateOnly"]!.GetValue<bool>());

        Assert.False(result.Valid);
        Assert.Equal("shop-1", result.ValidationResults![0].ResolvedSlug);
        Assert.Equal("Name is required", result.ValidationResults[1].Errors!.Single());
        Assert.Equal(1, result.Summary!.ToCreate);
        Assert.Equal(1, result.Summary.Errors);
    }

    [Fact]
    public async Task DestinationsImport_ValidateOnly_MapsValidationResults()
    {
        var (client, handler) = Create();
        handler.Respond("POST /api/destinations/import", """
            {"valid":true,
             "validationResults":[{"index":0,"name":"Orders","slug":"orders","status":"overwrite","errors":[],"warnings":[]}],
             "summary":{"total":1,"toCreate":0,"toOverwrite":1,"toSkip":0,"errors":0}}
            """);

        var result = await client.Destinations.ImportAsync(new ImportDestinationsRequest
        {
            Destinations = new List<CreateDestinationRequest> { new() { Name = "Orders", Url = "https://a.example" } },
            ConflictStrategy = "overwrite",
            ValidateOnly = true
        });

        Assert.True(result.Valid);
        Assert.Equal("overwrite", result.ValidationResults!.Single().Status);
        Assert.Equal(1, result.Summary!.ToOverwrite);
        Assert.Equal(1, result.Summary.Total);
    }

    [Fact]
    public async Task RoutesImport_SendsSlugs_AndMapsAppliedAndValidateOnlyAnswers()
    {
        var (client, handler) = Create();
        handler
            .Respond("POST /api/routes/import", """
                {"success":true,"imported":["r1"],"skipped":[],"errors":[{"name":"r2","error":"Source 'x' not found. Create it first."}],
                 "summary":{"total":2,"imported":1,"skipped":0,"errors":1}}
                """)
            .Respond("POST /api/routes/import", """
                {"success":true,"validateOnly":true,
                 "validationResults":[{"index":0,"name":"r1","status":"rename","errors":[],"warnings":[],"resolvedName":"r1 (2)"}],
                 "summary":{"total":1,"valid":1,"skipped":0,"overwrite":0,"errors":0}}
                """);

#pragma warning disable CS0618 // FilterName is the old alias of FilterSlug
        var route = new ExportedRoute
        {
            Name = "r1",
            SourceSlug = "shop",
            DestinationSlug = "orders",
            FilterName = "big-orders",
            TransformSlug = "to-crm",
            SchemaSlug = "order-v1"
        };
#pragma warning restore CS0618

        var applied = await client.Routes.ImportAsync(new ImportRoutesRequest { Routes = new() { route }, ConflictStrategy = "skip" });
        var validated = await client.Routes.ImportAsync(new ImportRoutesRequest { Routes = new() { route }, ConflictStrategy = "rename", ValidateOnly = true });

        var sentRoute = Sent(handler.Requests[0])["routes"]![0]!.AsObject();
        Assert.Equal("big-orders", sentRoute["filterSlug"]!.GetValue<string>());
        Assert.Equal("to-crm", sentRoute["transformSlug"]!.GetValue<string>());
        Assert.Equal("order-v1", sentRoute["schemaSlug"]!.GetValue<string>());
        Assert.False(sentRoute.ContainsKey("filterName"));

        Assert.Equal(new[] { "r1" }, applied.Imported);
        Assert.Equal("r2", applied.Errors!.Single().Name);
        Assert.Equal(1, applied.Summary!.Errors);

        Assert.True(validated.ValidateOnly);
        Assert.Equal("r1 (2)", validated.ValidationResults!.Single().ResolvedName);
        Assert.Equal(1, validated.Summary!.Valid);
    }

    [Fact]
    public async Task RoutesExport_ReadsSlugsAndFilterConditions()
    {
        var (client, handler) = Create();
        handler.Respond("GET /api/routes/export", """
            {"version":"1.0","exportedAt":"2026-10-20T10:00:00.000Z","organizationSlug":"acme",
             "routes":[{"name":"r1","sourceSlug":"shop","destinationSlug":"orders","filterSlug":"big","transformSlug":null,
                        "schemaSlug":null,"filterConditions":[{"field":"total","operator":"gt","value":100}],"filterLogic":"AND",
                        "priority":100,"isActive":true,"notifyOnFailure":false,"notifyOnSuccess":false,"notifyOnRecovery":false,
                        "notifyEmails":null,"failureThreshold":5,"failoverDestinationSlugs":[],"failoverAfterAttempts":3,
                        "circuitCooldownSeconds":300,"circuitFailureThreshold":5,"circuitProbeSuccessThreshold":2}]}
            """);

        var export = await client.Routes.ExportAsync();
        var r = export.Routes!.Single();

        Assert.Equal("big", r.FilterSlug);
        Assert.Equal("AND", r.FilterLogic);
        Assert.Equal("total", r.FilterConditions!.Single().Field);
    }

    // ---- destinations test ---------------------------------------------------------------------

    [Fact]
    public async Task DestinationTest_MapsStatusAndLatency()
    {
        var (client, handler) = Create();
        handler.Respond("POST /api/destinations/dst_1/test", """{"success":true,"status":200,"latencyMs":143,"responseBody":"ok"}""");

        var result = await client.Destinations.TestAsync("dst_1");

        Assert.True(result.Success);
        Assert.Equal(200, result.Status);
        Assert.Equal(143, result.LatencyMs);
        Assert.Equal("ok", result.ResponseBody);
    }
}

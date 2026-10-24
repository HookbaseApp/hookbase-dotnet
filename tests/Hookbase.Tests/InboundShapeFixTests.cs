using System.Net;
using System.Text.Json;
using Hookbase.Json;
using Hookbase.Models.Analytics;
using Hookbase.Models.Common;
using Hookbase.Models.Destinations;
using Hookbase.Models.Filters;
using Hookbase.Models.Routes;
using Hookbase.Models.Schemas;
using Hookbase.Models.Transforms;
using Hookbase.Models.Tunnels;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Inbound resources against the shapes the API actually sends and reads (api/src/routes/
/// destinations.ts, routes.ts, transforms.ts, tunnels.ts, schemas.ts, filters.ts, events.ts,
/// deliveries.ts, analytics.ts, cron.ts, apiKeys.ts).
/// </summary>
public class InboundShapeFixTests
{
    private const string Pagination = "\"pagination\":{\"total\":1,\"page\":1,\"pageSize\":20}";

    // --- Destinations: type names -------------------------------------------------------------

    [Theory]
    [InlineData(DestinationType.Http, "http")]
    [InlineData(DestinationType.S3, "s3")]
    [InlineData(DestinationType.R2, "r2")]
    [InlineData(DestinationType.Gcs, "gcs")]
    [InlineData(DestinationType.AzureBlob, "azure_blob")]
    [InlineData(DestinationType.Sqs, "sqs")]
    [InlineData(DestinationType.EventBridge, "eventbridge")]
    [InlineData(DestinationType.ServiceBus, "servicebus")]
    [InlineData(DestinationType.PubSub, "pubsub")]
    [InlineData(DestinationType.OciQueue, "oci_queue")]
    public async Task Every_destination_type_round_trips_under_the_API_name(DestinationType type, string wire)
    {
        var (client, t) = TestTransport.Create();
        t.Respond("{\"destination\":{\"id\":\"d1\",\"name\":\"x\",\"type\":\"" + wire + "\"}}", HttpStatusCode.Created);

        var created = await client.Destinations.CreateAsync(new CreateDestinationRequest { Name = "x", Type = type });

        Assert.Equal(wire, t.Last.Json.GetProperty("type").GetString());
        Assert.Equal(type, created.Type);
        Assert.Equal(wire, DestinationTypeJsonConverter.WireName(type));
    }

    [Fact]
    public async Task A_destination_list_with_queue_and_unknown_types_reads()
    {
        var (client, t) = TestTransport.Create();
        t.Respond($$"""
            {"destinations":[
              {"id":"d1","name":"a","type":"azure_blob"},
              {"id":"d2","name":"b","type":"oci_queue"},
              {"id":"d3","name":"c","type":"some_future_type"},
              {"id":"d4","name":"d","type":"AzureBlob"},
              {"id":"d5","name":"e","type":null}],{{Pagination}}}
            """);

        var page = await client.Destinations.ListAsync();

        Assert.Equal(new DestinationType?[] { DestinationType.AzureBlob, DestinationType.OciQueue, null, DestinationType.AzureBlob, null },
            page.Data.Select(d => d.Type));
    }

    // --- Routes ------------------------------------------------------------------------------

    [Fact]
    public async Task Route_filter_conditions_sent_as_JSON_text_are_parsed()
    {
        var (client, t) = TestTransport.Create();
        t.Respond($$"""
            {"routes":[
              {"id":"r1","filterConditions":"[{\"field\":\"type\",\"operator\":\"equals\",\"value\":\"order.created\"}]"},
              {"id":"r2","filterConditions":[{"field":"amount","operator":"gt","value":10}]},
              {"id":"r3","filterConditions":null},
              {"id":"r4","filterConditions":"not json"}],{{Pagination}}}
            """);

        var routes = (await client.Routes.ListAsync()).Data;

        var c1 = Assert.Single(routes[0].FilterConditions!);
        Assert.Equal("type", c1.Field);
        Assert.Equal("equals", c1.Operator);
        Assert.Equal("order.created", c1.Value.ToString());
        Assert.Equal("amount", Assert.Single(routes[1].FilterConditions!).Field);
        Assert.Null(routes[2].FilterConditions);
        Assert.Null(routes[3].FilterConditions);
    }

    [Fact]
    public async Task Route_bulk_operations_send_ids()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"deleted":2}""").Respond("""{"updated":1}""");

        await client.Routes.BulkDeleteAsync(new BulkDeleteRoutesRequest { RouteIds = new() { "r1", "r2" } });
        await client.Routes.BulkUpdateAsync(new BulkUpdateRoutesRequest { RouteIds = new() { "r1" }, IsActive = false });

        Assert.Equal("DELETE /api/routes/bulk", $"{t.Requests[0].Method} {t.Requests[0].Path}");
        Assert.Equal("""{"ids":["r1","r2"]}""", t.Requests[0].Body);
        Assert.Equal("PATCH /api/routes/bulk", $"{t.Requests[1].Method} {t.Requests[1].Path}");
        Assert.Equal("""{"ids":["r1"],"isActive":false}""", t.Requests[1].Body);
    }

    // --- Transforms --------------------------------------------------------------------------

    [Fact]
    public async Task Transform_create_maps_the_snake_case_response_fields()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"transform":{"id":"t1","name":"n","slug":"n","code":"$","transform_type":"liquid","input_format":"json","output_format":"text"}}""");

        var created = await client.Transforms.CreateAsync(new CreateTransformRequest { Name = "n", Code = "$", TransformType = "liquid" });

        Assert.Equal("liquid", created.TransformType);
        Assert.Equal("json", created.InputFormat);
        Assert.Equal("text", created.OutputFormat);
        Assert.Equal("t1", created.Id);
    }

    [Fact]
    public async Task Transform_update_reads_the_transform_back_after_the_success_only_PATCH()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"success":true}""")
         .Respond("""{"transform":{"id":"t1","name":"renamed","code":"$","transformType":"jsonata","isActive":false}}""");

        var updated = await client.Transforms.UpdateAsync("t1", new UpdateTransformRequest { Name = "renamed", IsActive = false });

        Assert.Equal(new[] { "PATCH /api/transforms/t1", "GET /api/transforms/t1" }, t.Requests.Select(r => $"{r.Method} {r.Path}"));
        Assert.Equal("""{"name":"renamed","isActive":false}""", t.Requests[0].Body);
        Assert.Equal("renamed", updated.Name);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task Transform_test_sends_the_engine_and_formats()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"success":true,"result":"hi","contentType":"text/plain"}""");

        var result = await client.Transforms.TestAsync(new TestTransformRequest
        {
            Code = "{{ name }}", Payload = new() { ["name"] = "hi" }, TransformType = "liquid", OutputFormat = "text"
        });

        Assert.Equal("""{"code":"{{ name }}","payload":{"name":"hi"},"transformType":"liquid","outputFormat":"text"}""", t.Last.Body);
        Assert.True(result.Success);
    }

    // --- Tunnels -----------------------------------------------------------------------------

    [Fact]
    public async Task Tunnel_create_surfaces_the_one_time_token_and_urls()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"tunnel":{"id":"tn1","name":"dev","subdomain":"abc","status":"disconnected","direction":"bidirectional",
              "allowedHosts":"[\"localhost\"]","auth_token":"tok_secret","authToken":null},
             "tunnelUrl":"https://www.hookbase.app/t/abc","wsUrl":"wss://www.hookbase.app/tunnels/abc/ws?tunnelId=tn1&token=tok_secret",
             "message":"Tunnel created."}
            """, HttpStatusCode.Created);

        var tunnel = await client.Tunnels.CreateAsync(new CreateTunnelRequest
        {
            Name = "dev", Direction = "bidirectional", AllowedHosts = new() { "localhost" }
        });

        Assert.Equal("""{"name":"dev","direction":"bidirectional","allowedHosts":["localhost"]}""", t.Last.Body);
        Assert.Equal("https://www.hookbase.app/t/abc", tunnel.TunnelUrl);
        Assert.StartsWith("wss://", tunnel.WsUrl);
        Assert.Equal("tok_secret", tunnel.AuthToken);
        Assert.Equal(new[] { "localhost" }, tunnel.AllowedHosts);
        Assert.Equal("bidirectional", tunnel.Direction);
    }

    // --- Schemas -----------------------------------------------------------------------------

    [Fact]
    public async Task Schema_validate_posts_the_payload_to_the_schema_route_and_reads_object_errors()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"valid":false,"errors":[{"path":"amount","message":"Expected number, got string"}],"payload":{"amount":"x"}}""");

        var result = await client.Schemas.ValidateAsync(new ValidateSchemaRequest
        {
            SchemaId = "sch 1", Payload = new() { ["amount"] = "x" }
        });

        Assert.Equal("POST /api/schemas/sch%201/validate", $"{t.Last.Method} {t.Last.Path}");
        Assert.Equal("""{"payload":{"amount":"x"}}""", t.Last.Body);
        Assert.False(result.Valid);
        var error = Assert.Single(result.Errors!);
        Assert.Equal("amount", error.Path);
        Assert.Equal("Expected number, got string", error.Message);
    }

    [Fact]
    public async Task Schema_create_maps_json_schema_and_list_text_parses()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"schema":{"id":"s1","name":"n","slug":"n","json_schema":{"type":"object"}}}""", HttpStatusCode.Created)
         .Respond("""{"schemas":[{"id":"s1","name":"n","jsonSchema":"{\"type\":\"object\"}"}]}""");

        var created = await client.Schemas.CreateAsync(new CreateSchemaRequest { Name = "n", JsonSchema = new { type = "object" } });
        var listed = Assert.Single((await client.Schemas.ListAsync()).Data);

        Assert.Equal("object", created.JsonSchemaElement!.Value.GetProperty("type").GetString());
        Assert.Equal("n", created.Slug);
        Assert.Equal("object", listed.JsonSchemaElement!.Value.GetProperty("type").GetString());
    }

    // --- Filters -----------------------------------------------------------------------------

    [Fact]
    public async Task Filter_test_reads_matches_results_and_logic()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"matches":true,"logic":"OR","results":[
              {"field":"type","operator":"equals","value":"a","fieldValue":"a","passed":true},
              {"field":"n","operator":"gt","value":5,"fieldValue":null,"passed":false}]}
            """);

        var result = await client.Filters.TestAsync(new TestFilterRequest
        {
            Conditions = new() { new FilterConditionInput { Field = "type", Operator = "equals", Value = "a" } },
            Logic = "OR",
            Payload = new() { ["type"] = "a" }
        });

        Assert.True(result.Matched);
        Assert.Equal("OR", result.Logic);
        Assert.Equal(2, result.Results!.Count);
        Assert.True(result.Results[0].Passed);
        Assert.False(result.Results[1].Passed);
        Assert.Null(result.Results[1].FieldValue);
    }

    // --- Status filters ----------------------------------------------------------------------

    [Theory]
    [InlineData(InboundEventStatus.Delivered, "delivered")]
    [InlineData(InboundEventStatus.Failed, "failed")]
    [InlineData(InboundEventStatus.Pending, "pending")]
    [InlineData(InboundEventStatus.Partial, "partial")]
    [InlineData(InboundEventStatus.Duplicate, "duplicate")]
    [InlineData(InboundEventStatus.NoRoutes, "no_routes")]
    public async Task Event_status_filters_use_the_API_names(InboundEventStatus status, string wire)
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"events":[],"limit":50,"hasMore":false}""");
        await client.Events.ListPageAsync(status: status);
        Assert.Contains($"status={wire}", t.Last.Query);
    }

    [Theory]
    [InlineData(DeliveryStatus.Pending, "pending")]
    [InlineData(DeliveryStatus.Delivered, "delivered")]
    [InlineData(DeliveryStatus.Failed, "failed")]
    [InlineData(DeliveryStatus.FailedOver, "failed_over")]
    [InlineData(DeliveryStatus.SchemaFailed, "schema_failed")]
    [InlineData(DeliveryStatus.Throttled, "throttled")]
    public async Task Delivery_status_filters_use_the_API_names(DeliveryStatus status, string wire)
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"deliveries":[],"limit":20,"offset":0}""");
        await client.Deliveries.ListAsync(status: status);
        Assert.Contains($"status={wire}", t.Last.Query);
    }

    // --- Event debug -------------------------------------------------------------------------

    [Fact]
    public async Task Event_debug_reads_curl_headers_and_payload_from_where_the_API_puts_them()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"event":{"id":"e1","sourceId":"s1","sourceSlug":"stripe","provider":"stripe","eventType":"charge.succeeded",
               "signatureValid":false,"signatureIssue":"unsigned","receivedAt":"2026-10-20T10:00:00.000Z"},
             "request":{"headers":{"content-type":"application/json"},"payloadSize":12,"contentType":"application/json"},
             "payload":{"a":1},"transient":false,"rawPayload":"{\"a\":1}",
             "deliveries":[{"id":"d1","status":"failed"}],"timing":{},"filterEvaluations":[],
             "recentConfigChanges":{"sinceDays":7,"entries":[]},
             "debug":{"curlCommand":"curl -X POST https://api.hookbase.app/ingest/acme/stripe"}}
            """);

        var info = await client.Events.DebugAsync("e1");

        Assert.Equal("/api/events/e1/debug", t.Last.Path);
        Assert.StartsWith("curl -X POST", info.CurlCommand);
        Assert.Equal("application/json", info.Event!.Headers!["content-type"]);
        Assert.Equal(1, ((JsonElement)info.Event.Payload!).GetProperty("a").GetInt32());
        Assert.Equal("unsigned", info.Event.SignatureIssue);
        Assert.Equal(12, info.Request!.PayloadSize);
        Assert.Equal(1, info.Deliveries!.Value.GetArrayLength());
        Assert.False(info.Transient);
    }

    // --- Analytics ---------------------------------------------------------------------------

    [Fact]
    public async Task Analytics_dashboard_reads_the_overview_and_sends_scope()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"overview":{"total_events":120,"successful_deliveries":90,"failed_deliveries":10,"avg_latency":42.5,"source_count":3,"destination_count":2},
             "timeline":[{"period":"2026-10-20","events":120,"successful":90,"failed":10}],
             "latency":{"percentiles":{"p50":1,"p75":2,"p90":3,"p95":4,"p99":5}},"latencyDistribution":[],"latencyTrends":[],
             "errors":{},"retries":{},"sources":[],"destinations":[],"routes":[{"id":"r1"},{"id":"r2"}],
             "payloadSizes":null,"range":"7d","interval":"day"}
            """);

        var data = await client.Analytics.DashboardAsync(new DashboardOptions
        {
            Range = "7d", TimeZone = "America/Chicago", SourceId = "s1"
        });

        Assert.Equal("?range=7d&tz=America/Chicago&sourceId=s1", t.Last.Query);
        Assert.Equal(120, data.EventsReceived);
        Assert.Equal(90, data.DeliveriesCompleted);
        Assert.Equal(90.0, data.DeliverySuccessRate, 3);
        Assert.Equal(3, data.ActiveSources);
        Assert.Equal(2, data.ActiveDestinations);
        Assert.Equal(2, data.ActiveRoutes);
        Assert.Equal(42.5, data.Overview!.AvgLatency);
        Assert.Equal("day", data.Interval);
        Assert.Single(data.Timeline!);
    }

    [Fact]
    public async Task Analytics_dashboard_with_no_deliveries_has_a_zero_success_rate()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"overview":{"total_events":0,"successful_deliveries":0,"failed_deliveries":0,"avg_latency":null},"timeline":[]}""");

        var data = await client.Analytics.DashboardAsync();

        Assert.Equal(string.Empty, t.Last.Query);
        Assert.Equal(0, data.DeliverySuccessRate);
        Assert.Equal(0, data.ActiveRoutes);
    }

    // --- Cron --------------------------------------------------------------------------------

    [Fact]
    public async Task Cron_executions_list_and_failed_trigger_expose_the_error()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"executions":[{"id":"x1","cronJobId":"c1","status":"failed","responseStatus":null,"responseBody":null,
              "errorMessage":"timeout","latencyMs":30000,"startedAt":"2026-10-20T10:00:00Z","completedAt":"2026-10-20T10:00:30Z"}]}
            """)
         .Respond("""{"success":false,"execution":{"id":"x2","status":"failed","error":"ECONNREFUSED","latencyMs":3}}""");

        var executions = await client.CronJobs.ListExecutionsAsync("c1", limit: 5);
        var triggered = await client.CronJobs.TriggerAsync("c1");

        Assert.Equal("GET /api/cron/c1/executions", $"{t.Requests[0].Method} {t.Requests[0].Path}");
        Assert.Equal("?limit=5", t.Requests[0].Query);
        Assert.Equal("timeout", Assert.Single(executions).ErrorMessage);
        Assert.Equal("ECONNREFUSED", triggered.ErrorMessage);
        Assert.Equal("ECONNREFUSED", triggered.Error);
    }

    // --- API keys ----------------------------------------------------------------------------

    [Fact]
    public async Task Api_key_scopes_sent_as_JSON_text_parse_into_a_list()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"apiKeys":[{"id":"k1","name":"a","keyPrefix":"whr_ab","scopes":"[\"read\",\"write\"]"},
                        {"id":"k2","name":"b","keyPrefix":"whr_cd","scopes":["read"]},
                        {"id":"k3","name":"c","keyPrefix":"whr_ef","scopes":null}]}
            """);

        var keys = await client.ApiKeys.ListAsync();

        Assert.Equal(new[] { "read", "write" }, keys[0].ScopeList);
        Assert.Equal(new[] { "read" }, keys[1].ScopeList);
        Assert.Empty(keys[2].ScopeList);
    }

    // --- Converter edge cases ----------------------------------------------------------------

    [Fact]
    public void Text_or_value_converter_writes_plain_JSON()
    {
        var route = new Route { FilterConditions = new() { new FilterCondition { Field = "a", Operator = "equals", Value = "b" } } };
        var json = JsonSerializer.Serialize(route, HookbaseJson.Options);
        Assert.Contains("\"filterConditions\":[{\"field\":\"a\",\"operator\":\"equals\",\"value\":\"b\"}]", json);
    }

    [Fact]
    public void Destination_type_converter_rejects_a_non_string_token()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Destination>("""{"type":5}""", HookbaseJson.Options));
    }
}

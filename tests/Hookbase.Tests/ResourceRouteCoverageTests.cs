using System.Net;
using Hookbase.Models.Applications;
using Hookbase.Models.CronJobs;
using Hookbase.Models.Destinations;
using Hookbase.Models.Endpoints;
using Hookbase.Models.EventTypes;
using Hookbase.Models.Filters;
using Hookbase.Models.PortalTokens;
using Hookbase.Models.Routes;
using Hookbase.Models.Schemas;
using Hookbase.Models.Subscriptions;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Method, path, query and body for every resource method no other test calls, answered with
/// the wrapper each API route returns (api/src/routes/*.ts), so a path or wrapper drift fails here.
/// </summary>
public class ResourceRouteCoverageTests
{
    private const string Ok = """{"success":true}""";
    private const string OffsetPagination = "\"pagination\":{\"total\":1,\"page\":1,\"pageSize\":20}";
    private const string CursorPagination = "\"pagination\":{\"hasMore\":false,\"nextCursor\":null}";
    private const string App = """{"id":"app_1","organizationId":"org_1","name":"Acme","externalId":"cust_1"}""";

    private static string Calls(TestTransport t) => string.Join("\n", t.Requests.Select(r => $"{r.Method} {r.Path}{r.Query}"));

    [Fact]
    public async Task Applications_crud_and_upsert()
    {
        var (client, t) = TestTransport.Create();
        t.Respond($$"""{"data":{{App}}}""")
         .Respond($$"""{"data":{{App}}}""", HttpStatusCode.Created)
         .Respond($$"""{"data":{{App}}}""")
         .Respond($$"""{"data":{{App}},"created":true}""", HttpStatusCode.Created)
         .Respond(Ok);

        var got = await client.Applications.GetAsync("app_1");
        await client.Applications.CreateAsync(new CreateApplicationRequest { Name = "Acme", ExternalId = "cust_1" });
        await client.Applications.UpdateAsync("app_1", new UpdateApplicationRequest { Name = "Acme 2" });
        var upserted = await client.Applications.GetOrCreateAsync(new GetOrCreateApplicationRequest { ExternalId = "cust_1", Name = "Acme" });
        await client.Applications.DeleteAsync("app_1");

        Assert.Equal("""
            GET /api/webhook-applications/app_1
            POST /api/webhook-applications
            PATCH /api/webhook-applications/app_1
            PUT /api/webhook-applications/upsert
            DELETE /api/webhook-applications/app_1
            """.ReplaceLineEndings("\n"), Calls(t));
        Assert.Equal("""{"name":"Acme","externalId":"cust_1"}""", t.Requests[1].Body);
        Assert.Equal("""{"name":"Acme 2"}""", t.Requests[2].Body);
        Assert.Equal("cust_1", t.Requests[3].Json.GetProperty("externalId").GetString());
        Assert.Equal("Acme", got.Name);
        Assert.Equal("app_1", upserted.Id);
    }

    [Fact]
    public async Task Cron_jobs_and_groups()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"cronJobs":[{"id":"c1","name":"n","cronExpression":"* * * * *","url":"https://a.example"}]}""")
         .Respond("""{"cronJob":{"id":"c1","name":"n"}}""")
         .Respond("""{"cronJob":{"id":"c2","name":"n","cronExpression":"0 * * * *","url":"https://a.example","nextRunAt":"2026-10-20T11:00:00Z"}}""")
         .Respond(Ok)
         .Respond(Ok)
         .Respond("""{"groups":[{"id":"g1","name":"G"}]}""")
         .Respond("""{"group":{"id":"g2","name":"New"}}""", HttpStatusCode.Created);

        var list = await client.CronJobs.ListAsync();
        await client.CronJobs.GetAsync("c1");
        var created = await client.CronJobs.CreateAsync(new CreateCronJobRequest { Name = "n", Url = "https://a.example", CronExpression = "0 * * * *" });
        await client.CronJobs.UpdateAsync("c1", new UpdateCronJobRequest { Name = "m" });
        await client.CronJobs.DeleteAsync("c1");
        var groups = await client.CronJobs.ListGroupsAsync();
        var group = await client.CronJobs.CreateGroupAsync(new CreateCronGroupRequest { Name = "New" });

        Assert.Equal("""
            GET /api/cron?page=1&pageSize=20
            GET /api/cron/c1
            POST /api/cron
            PATCH /api/cron/c1
            DELETE /api/cron/c1
            GET /api/cron-groups
            POST /api/cron-groups
            """.ReplaceLineEndings("\n"), Calls(t));
        Assert.Equal("""{"name":"m"}""", t.Requests[3].Body);
        Assert.Single(list.Data);
        Assert.Equal("c2", created.Id);
        Assert.Equal("g1", Assert.Single(groups).Id);
        Assert.Equal("g2", group.Id);
    }

    [Fact]
    public async Task Filters_crud()
    {
        var (client, t) = TestTransport.Create();
        t.Respond($$"""{"filters":[{"id":"f1","name":"n","conditions":[],"logic":"AND"}],{{OffsetPagination}}}""")
         .Respond("""{"filter":{"id":"f1","name":"n"}}""")
         .Respond("""{"filter":{"id":"f2","name":"n","slug":"n","conditions":[{"field":"a","operator":"equals","value":1}],"logic":"OR"}}""")
         .Respond(Ok)
         .Respond(Ok);

        await client.Filters.ListAsync();
        await client.Filters.GetAsync("f1");
        var created = await client.Filters.CreateAsync(new CreateFilterRequest
        {
            Name = "n", Conditions = new() { new FilterConditionInput { Field = "a", Operator = "equals", Value = 1 } }, Logic = "OR"
        });
        await client.Filters.UpdateAsync("f1", new UpdateFilterRequest { Logic = "AND" });
        await client.Filters.DeleteAsync("f1");

        Assert.Equal("""
            GET /api/filters?page=1&pageSize=20
            GET /api/filters/f1
            POST /api/filters
            PATCH /api/filters/f1
            DELETE /api/filters/f1
            """.ReplaceLineEndings("\n"), Calls(t));
        Assert.Equal("""{"name":"n","conditions":[{"field":"a","operator":"equals","value":1}],"logic":"OR"}""", t.Requests[2].Body);
        Assert.Equal("f2", created.Id);
    }

    [Fact]
    public async Task Portal_tokens()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"data":{"id":"pt1","token":"whpt_once","name":"n","scopes":["read"],"expiresAt":"2026-11-20T00:00:00Z"}}""", HttpStatusCode.Created)
         .Respond("""{"data":[{"id":"pt1","name":"n"}]}""")
         .Respond(Ok);

        var created = await client.PortalTokens.CreateAsync("app_1", new CreatePortalTokenRequest { Name = "n", Scopes = new() { "read" } });
        var list = await client.PortalTokens.ListAsync("app_1");
        await client.PortalTokens.RevokeAsync("app_1", "pt1");

        Assert.Equal("""
            POST /api/portal/webhook-applications/app_1/tokens
            GET /api/portal/webhook-applications/app_1/tokens
            DELETE /api/portal/tokens/pt1
            """.ReplaceLineEndings("\n"), Calls(t));
        Assert.Equal("whpt_once", created.Token);
        Assert.Single(list.Data);
        Assert.False(list.HasMore);
    }

    [Fact]
    public async Task Event_types_crud()
    {
        var (client, t) = TestTransport.Create();
        const string row = """{"data":{"id":"et1","name":"order.created","displayName":"Order created","isEnabled":true}}""";
        t.Respond(row).Respond(row, HttpStatusCode.Created).Respond(row).Respond(Ok);

        await client.EventTypes.GetAsync("et1");
        await client.EventTypes.CreateAsync(new CreateEventTypeRequest { Name = "order.created" });
        await client.EventTypes.UpdateAsync("et1", new UpdateEventTypeRequest { DisplayName = "Order" });
        await client.EventTypes.DeleteAsync("et1");

        Assert.Equal("""
            GET /api/event-types/et1
            POST /api/event-types
            PATCH /api/event-types/et1
            DELETE /api/event-types/et1
            """.ReplaceLineEndings("\n"), Calls(t));
        Assert.Equal("""{"name":"order.created"}""", t.Requests[1].Body);
        Assert.Equal("""{"displayName":"Order"}""", t.Requests[2].Body);
    }

    [Fact]
    public async Task Endpoints_create_returns_the_one_time_secret_update_and_delete()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"data":{"id":"ep1","applicationId":"app_1","url":"https://a.example/hook","secret":"whsec_once"},"message":"Save the signing secret now."}""", HttpStatusCode.Created)
         .Respond("""{"data":{"id":"ep1","applicationId":"app_1","url":"https://b.example/hook"}}""")
         .Respond(Ok);

        var created = await client.Endpoints.CreateAsync(new CreateEndpointRequest { ApplicationId = "app_1", Url = "https://a.example/hook" });
        var updated = await client.Endpoints.UpdateAsync("ep1", new UpdateEndpointRequest { Url = "https://b.example/hook" });
        await client.Endpoints.DeleteAsync("ep1");

        Assert.Equal("""
            POST /api/webhook-endpoints
            PATCH /api/webhook-endpoints/ep1
            DELETE /api/webhook-endpoints/ep1
            """.ReplaceLineEndings("\n"), Calls(t));
        Assert.Equal("whsec_once", created.Secret);
        Assert.Equal("https://b.example/hook", updated.Url);
    }

    [Fact]
    public async Task Routes_create_update_delete()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"route":{"id":"r1","name":"n","sourceId":"s1","destinationId":"d1","filterConditions":[{"field":"a","operator":"equals","value":"b"}],"filterLogic":"AND"}}""", HttpStatusCode.Created)
         .Respond(Ok)
         .Respond(Ok);

        var created = await client.Routes.CreateAsync(new CreateRouteRequest { Name = "n", SourceId = "s1", DestinationId = "d1" });
        await client.Routes.UpdateAsync("r1", new UpdateRouteRequest { Name = "m" });
        await client.Routes.DeleteAsync("r1");

        Assert.Equal("""
            POST /api/routes
            PATCH /api/routes/r1
            DELETE /api/routes/r1
            """.ReplaceLineEndings("\n"), Calls(t));
        Assert.Equal("a", Assert.Single(created.FilterConditions!).Field);
        Assert.Equal("""{"name":"m"}""", t.Requests[1].Body);
    }

    [Fact]
    public async Task Destinations_get_update_delete_bulk_export_and_list_all()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"destination":{"id":"d1","name":"n","type":"sqs"}}""")
         .Respond(Ok)
         .Respond(Ok)
         .Respond("""{"success":true,"deleted":2}""")
         .Respond("""{"version":"1.0","exportedAt":"2026-10-20T10:00:00Z","organizationSlug":"acme","destinations":[{"id":"d1","name":"n"}]}""")
         .Respond("""{"destinations":[{"id":"d1","name":"n"}],"pagination":{"total":1,"page":1,"pageSize":100}}""");

        var got = await client.Destinations.GetAsync("d1");
        await client.Destinations.UpdateAsync("d1", new UpdateDestinationRequest { Name = "m" });
        await client.Destinations.DeleteAsync("d1");
        var bulk = await client.Destinations.BulkDeleteAsync(new List<string> { "d1", "d2" });
        var export = await client.Destinations.ExportAsync(new List<string> { "d1", "d2" });
        var all = new List<Destination>();
        await foreach (var d in client.Destinations.ListAllAsync()) all.Add(d);

        Assert.Equal("""
            GET /api/destinations/d1
            PATCH /api/destinations/d1
            DELETE /api/destinations/d1
            DELETE /api/destinations/bulk
            GET /api/destinations/export?includeSensitive=false&ids=d1,d2
            GET /api/destinations?page=1&pageSize=100
            """.ReplaceLineEndings("\n"), Calls(t));
        Assert.Equal(DestinationType.Sqs, got.Type);
        Assert.Equal("""{"ids":["d1","d2"]}""", t.Requests[3].Body);
        Assert.Equal(2, bulk.Deleted);
        Assert.Equal("acme", export.OrganizationSlug);
        Assert.Single(all);
    }

    [Fact]
    public async Task Sources_list_delete_reveal_export_and_list_all()
    {
        var (client, t) = TestTransport.Create();
        t.Respond($$"""{"sources":[{"id":"s1","name":"n","slug":"n"}],{{OffsetPagination}}}""")
         .Respond(Ok)
         .Respond("""{"signingSecret":"whsec_abc"}""")
         .Respond("""{"version":"1.0","organizationSlug":"acme","sources":[{"id":"s1","name":"n","slug":"n"}]}""")
         .Respond("""{"sources":[{"id":"s1","name":"n","slug":"n"}],"pagination":{"total":1,"page":1,"pageSize":100}}""");

        var page = await client.Sources.ListAsync(page: 2, pageSize: 10);
        await client.Sources.DeleteAsync("s1");
        var secret = await client.Sources.RevealSecretAsync("s1");
        var export = await client.Sources.ExportAsync();
        var all = new List<Hookbase.Models.Sources.Source>();
        await foreach (var s in client.Sources.ListAllAsync()) all.Add(s);

        Assert.Equal("GET /api/sources?page=2&pageSize=10", $"{t.Requests[0].Method} {t.Requests[0].Path}{t.Requests[0].Query}");
        Assert.Equal("DELETE /api/sources/s1", $"{t.Requests[1].Method} {t.Requests[1].Path}");
        Assert.Equal("GET /api/sources/s1/reveal-secret", $"{t.Requests[2].Method} {t.Requests[2].Path}");
        Assert.Equal("GET /api/sources/export", $"{t.Requests[3].Method} {t.Requests[3].Path}");
        Assert.Equal("GET /api/sources?page=1&pageSize=100", $"{t.Requests[4].Method} {t.Requests[4].Path}{t.Requests[4].Query}");
        Assert.Single(page.Data);
        Assert.Equal("whsec_abc", secret);
        Assert.Equal("acme", export.OrganizationSlug);
        Assert.Single(all);
    }

    [Fact]
    public async Task Subscriptions_list_bulk_and_delete()
    {
        var (client, t) = TestTransport.Create();
        t.Respond($$"""{"data":[{"id":"sub_1","endpointId":"ep1","eventTypeId":"et1","isEnabled":true}],{{CursorPagination}}}""")
         .Respond("""{"created":1,"skipped":1,"subscriptions":[{"id":"sub_2","endpointId":"ep1","eventTypeId":"et2","labelFilters":null,"isEnabled":true}]}""", HttpStatusCode.Created)
         .Respond(Ok);

        await client.Subscriptions.ListAsync(limit: 10, endpointId: "ep1");
        var bulk = await client.Subscriptions.BulkSubscribeAsync(new BulkSubscribeRequest
        {
            EndpointId = "ep1", EventTypeIds = new() { "et1", "et2" }, FilterExpression = "a = 1", EventVersion = "latest"
        });
        await client.Subscriptions.DeleteAsync("sub_1");

        Assert.Equal("GET /api/webhook-subscriptions?limit=10&endpointId=ep1", $"{t.Requests[0].Method} {t.Requests[0].Path}{t.Requests[0].Query}");
        Assert.Equal("POST /api/webhook-subscriptions/bulk", $"{t.Requests[1].Method} {t.Requests[1].Path}");
        Assert.Equal("""{"endpointId":"ep1","eventTypeIds":["et1","et2"],"filterExpression":"a = 1","eventVersion":"latest"}""", t.Requests[1].Body);
        Assert.Equal("DELETE /api/webhook-subscriptions/sub_1", $"{t.Requests[2].Method} {t.Requests[2].Path}");
        Assert.Equal(1, bulk.Created);
        Assert.Equal("sub_2", Assert.Single(bulk.Subscriptions!).Id);
    }

    [Fact]
    public async Task Tunnels_list_get_and_bulk_delete()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"tunnels":[{"id":"tn1","name":"dev","subdomain":"abc","status":"connected","auth_token":null,"allowedHosts":null}]}""")
         .Respond("""{"tunnel":{"id":"tn1","name":"dev","totalRequests":4,"lastConnectedAt":"2026-10-20T10:00:00Z"}}""")
         .Respond("""{"success":true,"deleted":1}""");

        var list = await client.Tunnels.ListAsync();
        var got = await client.Tunnels.GetAsync("tn1");
        var bulk = await client.Tunnels.BulkDeleteAsync(new List<string> { "tn1" });

        Assert.Equal("""
            GET /api/tunnels
            GET /api/tunnels/tn1
            DELETE /api/tunnels/bulk
            """.ReplaceLineEndings("\n"), Calls(t));
        Assert.Equal("""{"ids":["tn1"]}""", t.Requests[2].Body);
        Assert.Null(Assert.Single(list).AuthToken);
        Assert.Equal(4, got.TotalRequests);
        Assert.Equal(1, bulk.Deleted);
    }

    [Fact]
    public async Task Schemas_get_update_delete_and_transforms_list_delete()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"schema":{"id":"sc1","name":"n","jsonSchema":"{\"type\":\"object\"}"}}""")
         .Respond(Ok)
         .Respond(Ok)
         .Respond($$"""{"transforms":[{"id":"t1","name":"n","transformType":"jsonata"}],{{OffsetPagination}}}""")
         .Respond(Ok);

        var schema = await client.Schemas.GetAsync("sc1");
        await client.Schemas.UpdateAsync("sc1", new UpdateSchemaRequest { Name = "m" });
        await client.Schemas.DeleteAsync("sc1");
        var transforms = await client.Transforms.ListAsync();
        await client.Transforms.DeleteAsync("t1");

        Assert.Equal("""
            GET /api/schemas/sc1
            PUT /api/schemas/sc1
            DELETE /api/schemas/sc1
            GET /api/transforms?page=1&pageSize=20
            DELETE /api/transforms/t1
            """.ReplaceLineEndings("\n"), Calls(t));
        Assert.Equal("object", schema.JsonSchemaElement!.Value.GetProperty("type").GetString());
        Assert.Single(transforms.Data);
    }

    [Fact]
    public async Task Delivery_replay_and_dlq_delete()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"success":true,"newDeliveryId":"del_2"}""").Respond("""{"data":{"deleted":true}}""");

        await client.Deliveries.ReplayAsync("del_1");
        await client.Dlq.DeleteAsync("msg_1");

        Assert.Equal("""
            POST /api/deliveries/del_1/replay
            DELETE /api/outbound-messages/dlq/msg_1
            """.ReplaceLineEndings("\n"), Calls(t));
    }
}

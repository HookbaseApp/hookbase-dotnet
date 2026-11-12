using System.Net;
using System.Text.Json;
using Hookbase.Models.Deliveries;
using Hookbase.Models.Sources;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Replay with edits (api/src/routes/deliveries.ts, POST /:deliveryId/replay), transform engines
/// (routes/transforms.ts, GET /engines) and the source fields of api #131 (catalogProvider, list
/// fields as arrays).
/// </summary>
public class ReplayEnginesSourcesTests
{
    private const string ReplayResult = """
        {"deliveryId":"dlv_new","message":"Replay queued with modified payload, header overrides",
         "modified":true,"destinationOverridden":false,"transformOverridden":false,
         "transformPersisted":false,"headersOverridden":true,"targetDestinationId":"dst_1"}
        """;

    [Fact]
    public async Task Replay_with_a_patch_sends_only_the_set_fields_and_reads_the_result()
    {
        var (client, t) = TestTransport.Create();
        t.Respond(ReplayResult);

        var result = await client.Deliveries.ReplayAsync("dlv 1", new ReplayDeliveryRequest
        {
            PayloadPatch = JsonSerializer.SerializeToElement(new { type = "order.updated", note = (string?)null }),
            HeadersOverride = new() { ["X-Replay-Reason"] = "fixed" }
        });

        Assert.Equal("POST /api/deliveries/dlv%201/replay", $"{t.Last.Method} {t.Last.Path}");
        // The merge patch's null (remove "note") goes through untouched; unset members are not sent.
        Assert.Equal("""{"payloadPatch":{"type":"order.updated","note":null},"headersOverride":{"X-Replay-Reason":"fixed"}}""", t.Last.Body);

        Assert.Equal("dlv_new", result.DeliveryId);
        Assert.True(result.Modified);
        Assert.True(result.HeadersOverridden);
        Assert.False(result.DestinationOverridden);
        Assert.False(result.TransformPersisted);
        Assert.Equal("dst_1", result.TargetDestinationId);
    }

    [Fact]
    public async Task Replay_sends_every_field_under_its_API_name()
    {
        var (client, t) = TestTransport.Create();
        t.Respond(ReplayResult).Respond(ReplayResult);

        await client.Deliveries.ReplayAsync("dlv_1", new ReplayDeliveryRequest
        {
            ModifiedPayload = JsonSerializer.SerializeToElement(new[] { 1, 2 }),
            DestinationOverride = "staging-sink",
            TransformOverride = new ReplayTransformOverride { Code = "$", Type = "jsonata" },
            PersistTransform = false
        });
        Assert.Equal(
            """{"modifiedPayload":[1,2],"destinationOverride":"staging-sink","transformOverride":{"code":"$","type":"jsonata"},"persistTransform":false}""",
            t.Last.Body);

        // An empty request is a plain replay with an empty body.
        await client.Deliveries.ReplayAsync("dlv_1", new ReplayDeliveryRequest());
        Assert.Equal("{}", t.Last.Body);
    }

    [Fact]
    public async Task Replay_refuses_what_the_API_would_refuse_without_sending()
    {
        var (client, t) = TestTransport.Create();
        var obj = JsonSerializer.SerializeToElement(new { a = 1 });

        var both = await Assert.ThrowsAsync<ArgumentException>(() => client.Deliveries.ReplayAsync("dlv_1",
            new ReplayDeliveryRequest { ModifiedPayload = obj, PayloadPatch = obj }));
        Assert.Contains("not both", both.Message);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Deliveries.ReplayAsync("dlv_1",
            new ReplayDeliveryRequest { PayloadPatch = JsonSerializer.SerializeToElement(new[] { 1 }) }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Deliveries.ReplayAsync("dlv_1",
            new ReplayDeliveryRequest { PersistTransform = true }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.Deliveries.ReplayAsync("dlv_1", (ReplayDeliveryRequest)null!));

        Assert.Empty(t.Requests);
    }

    [Fact]
    public async Task A_default_JsonElement_counts_as_unset()
    {
        var (client, t) = TestTransport.Create();
        t.Respond(ReplayResult);

        await client.Deliveries.ReplayAsync("dlv_1", new ReplayDeliveryRequest
        {
            ModifiedPayload = default(JsonElement),
            PayloadPatch = JsonSerializer.SerializeToElement(new { a = 1 })
        });
        Assert.Equal("""{"payloadPatch":{"a":1}}""", t.Last.Body);
    }

    [Fact]
    public async Task The_plain_replay_overload_still_binds_and_sends_no_body()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"success":true,"deliveryId":"dlv_new"}""").Respond("""{"success":true}""");

        // Both calls resolve to ReplayAsync(string, CancellationToken), which returns a plain Task.
        Task plain = client.Deliveries.ReplayAsync("dlv_1");
        await plain;
        Assert.Null(t.Last.Body);
        await client.Deliveries.ReplayAsync("dlv_1", CancellationToken.None);
        Assert.Null(t.Last.Body);
    }

    [Fact]
    public async Task EnginesAsync_reads_the_transform_catalog()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"engines":[
              {"type":"jsonata","label":"JSONata","description":"Query and transformation language for JSON.",
               "fileExtension":"jsonata","starterCode":"{ \"id\": id }","aiGenerate":true,"available":true},
              {"type":"jq","label":"jq","description":"jq filters.","fileExtension":"jq","starterCode":".",
               "aiGenerate":false,"available":false}],
             "contentFormats":["json","xml","text"]}
            """);

        var engines = await client.Transforms.EnginesAsync();
        Assert.Equal("GET /api/transforms/engines", $"{t.Last.Method} {t.Last.Path}");
        Assert.Equal(2, engines.Engines.Count);
        Assert.Equal("jsonata", engines.Engines[0].Type);
        Assert.Equal("{ \"id\": id }", engines.Engines[0].StarterCode);
        Assert.True(engines.Engines[0].AiGenerate);
        Assert.False(engines.Engines[1].Available);
        Assert.Equal(new[] { "json", "xml", "text" }, engines.ContentFormats);
    }

    [Fact]
    public async Task Source_lists_read_as_arrays_and_catalogProvider_is_read_and_sent_on_create()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"source":{"id":"src_1","name":"Shop","slug":"shop","provider":"custom","catalogProvider":"shopify-flow",
             "ipFilterMode":"allowlist","ipAllowlist":["203.0.113.0/24","198.51.100.7"],"ipDenylist":[],
             "encryptFields":["card.number"],"maskFields":["customer.email","customer.phone"]}}
            """)
         .Respond("""
            {"source":{"id":"src_2","name":"Shop","slug":"shop","provider":"custom","catalogProvider":"shopify-flow",
             "ipAllowlist":["203.0.113.0/24"],"ipDenylist":[],"encryptFields":[],"maskFields":[],
             "signingSecret":"whsec_s"}}
            """, HttpStatusCode.Created)
         .Respond("""{"sources":[{"id":"src_1","slug":"shop","catalogProvider":null,"ipAllowlist":[],"ipDenylist":[],"encryptFields":[],"maskFields":[]}],"pagination":{"total":1,"page":1,"pageSize":20}}""");

        var source = await client.Sources.GetAsync("src_1");
        Assert.Equal("shopify-flow", source.CatalogProvider);
        Assert.Equal(new[] { "203.0.113.0/24", "198.51.100.7" }, source.IpAllowlist);
        Assert.Empty(source.IpDenylist);
        Assert.Equal(new[] { "card.number" }, source.EncryptFields);
        Assert.Equal(new[] { "customer.email", "customer.phone" }, source.MaskFields);

        var created = await client.Sources.CreateAsync(new CreateSourceRequest
        {
            Name = "Shop",
            Slug = "shop",
            Provider = "custom",
            CatalogProvider = "shopify-flow",
            IpFilterMode = "allowlist",
            IpAllowlist = new() { "203.0.113.0/24" }
        });
        var body = t.Last.Json;
        Assert.Equal("shopify-flow", body.GetProperty("catalogProvider").GetString());
        Assert.Equal("203.0.113.0/24", body.GetProperty("ipAllowlist")[0].GetString());
        Assert.Equal("shopify-flow", created.CatalogProvider);

        var page = await client.Sources.ListAsync();
        Assert.Null(page.Data[0].CatalogProvider);
        Assert.Empty(page.Data[0].IpAllowlist);
    }

    [Fact]
    public void An_update_never_sends_catalogProvider()
    {
        // The API's update schema is strict and has no catalogProvider; the update request has no
        // such member, so no update body can carry it.
        Assert.Null(typeof(UpdateSourceRequest).GetProperty("CatalogProvider"));
        var body = JsonSerializer.Serialize(new UpdateSourceRequest { Name = "x" }, Hookbase.Json.HookbaseJson.Options);
        Assert.DoesNotContain("catalogProvider", body);
    }
}

using System.Net;
using System.Text.Json;
using Hookbase.Json;
using Hookbase.Models.Destinations;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Bridge destinations: the <c>bridge</c> type, its config and its dry-run test, against
/// api/src/routes/destinations.ts and api/src/types/warehouse.ts (<c>bridgeConfigSchema</c>).
/// </summary>
public class DestinationBridgeTests
{
    // GET /api/destinations/:id for a bridge: url is "", config is the stored bridge config.
    private const string BridgeRow = """
        {"id":"dst_b","organizationId":"org_1","name":"Bridge","slug":"bridge","url":"","method":"POST",
         "type":"bridge","config":{"eventTypeSource":"field","eventTypeField":"data.type","eventTypePrefix":"stripe."},
         "isActive":true,"useStaticIp":false,"hasSigningSecret":false,"timeoutMs":30000}
        """;

    [Fact]
    public void A_bridge_destination_reads_as_Bridge_and_a_create_request_sends_bridge()
    {
        var destination = JsonSerializer.Deserialize<Destination>(BridgeRow, HookbaseJson.Options)!;
        Assert.Equal(DestinationType.Bridge, destination.Type);
        Assert.Equal("", destination.Url);

        var body = JsonSerializer.SerializeToElement(new CreateDestinationRequest
        {
            Name = "Bridge",
            Type = DestinationType.Bridge,
            Config = BridgeConfig.Fixed("order.created")
        }, HookbaseJson.Options);

        Assert.Equal("bridge", body.GetProperty("type").GetString());
        Assert.Equal("""{"eventTypeSource":"fixed","eventType":"order.created"}""", body.GetProperty("config").GetRawText());
    }

    [Theory]
    [InlineData(DestinationType.Http, "http")]
    [InlineData(DestinationType.Sqs, "sqs")]
    [InlineData(DestinationType.EventBridge, "eventbridge")]
    [InlineData(DestinationType.ServiceBus, "servicebus")]
    [InlineData(DestinationType.PubSub, "pubsub")]
    [InlineData(DestinationType.OciQueue, "oci_queue")]
    [InlineData(DestinationType.S3, "s3")]
    [InlineData(DestinationType.R2, "r2")]
    [InlineData(DestinationType.Gcs, "gcs")]
    [InlineData(DestinationType.AzureBlob, "azure_blob")]
    [InlineData(DestinationType.Bridge, "bridge")]
    public void Every_API_destination_type_round_trips_under_its_wire_name(DestinationType type, string wire)
    {
        Assert.Equal(wire, DestinationTypeJsonConverter.WireName(type));

        var read = JsonSerializer.Deserialize<Destination>($$"""{"type":"{{wire}}"}""", HookbaseJson.Options)!;
        Assert.Equal(type, read.Type);
    }

    [Fact]
    public void Every_enum_member_has_a_wire_name()
    {
        // The API's DESTINATION_TYPES has 11 entries; a member without a wire name would throw on write.
        Assert.Equal(11, Enum.GetValues<DestinationType>().Length);
        foreach (var type in Enum.GetValues<DestinationType>())
        {
            Assert.False(string.IsNullOrEmpty(DestinationTypeJsonConverter.WireName(type)));
        }
    }

    [Fact]
    public void GetBridgeConfig_reads_the_config_of_a_bridge_and_null_for_other_types()
    {
        var bridge = JsonSerializer.Deserialize<Destination>(BridgeRow, HookbaseJson.Options)!;
        var config = bridge.GetBridgeConfig()!;
        Assert.Equal(BridgeEventTypeSource.Field, config.EventTypeSource);
        Assert.Equal("data.type", config.EventTypeField);
        Assert.Equal("stripe.", config.EventTypePrefix);
        Assert.Null(config.EventType);

        // fn_dest_list returns config as text on older deployments; both forms read.
        var asText = JsonSerializer.Deserialize<Destination>(
            """{"type":"bridge","config":"{\"eventTypeSource\":\"fixed\",\"eventType\":\"a.b\"}"}""", HookbaseJson.Options)!;
        Assert.Equal("a.b", asText.GetBridgeConfig()!.EventType);

        var http = JsonSerializer.Deserialize<Destination>("""{"type":"http","config":null}""", HookbaseJson.Options)!;
        Assert.Null(http.GetBridgeConfig());
    }

    [Fact]
    public void BridgeConfig_factories_send_only_the_keys_the_strict_schema_allows()
    {
        Assert.Equal("""{"eventTypeSource":"fixed","eventType":"order.created","applicationId":"app_1"}""",
            JsonSerializer.Serialize(BridgeConfig.Fixed("order.created") with { ApplicationId = "app_1" }, HookbaseJson.Options));
        Assert.Equal("""{"eventTypeSource":"field","eventTypeField":"type","eventTypePrefix":"gh.","allowUnverifiedSources":true}""",
            JsonSerializer.Serialize(BridgeConfig.FromField("type", "gh.") with { AllowUnverifiedSources = true }, HookbaseJson.Options));

        var element = BridgeConfig.Fixed("a.b").ToJsonElement();
        Assert.Equal("fixed", element.GetProperty("eventTypeSource").GetString());
    }

    [Fact]
    public async Task Test_with_a_payload_sends_it_and_reads_the_bridge_dry_run()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"success":true,"dryRun":true,"eventType":"stripe.invoice.paid","eventTypeExists":true,"matchingEndpoints":2}""")
         .Respond("""{"success":false,"dryRun":true,"eventType":null,"eventTypeExists":false,"matchingEndpoints":0,"error":"Field data.type is missing"}""")
         .Respond("""{"success":true,"status":200,"latencyMs":12,"responseBody":"ok"}""");

        var payload = JsonSerializer.SerializeToElement(new { data = new { type = "invoice.paid" } });
        var dryRun = await client.Destinations.TestAsync("dst_b", payload);
        Assert.Equal("POST /api/destinations/dst_b/test", $"{t.Last.Method} {t.Last.Path}");
        Assert.Equal("""{"payload":{"data":{"type":"invoice.paid"}}}""", t.Last.Body);
        Assert.True(dryRun.Success);
        Assert.True(dryRun.DryRun);
        Assert.Equal("stripe.invoice.paid", dryRun.EventType);
        Assert.True(dryRun.EventTypeExists);
        Assert.Equal(2, dryRun.MatchingEndpoints);

        var unresolved = await client.Destinations.TestAsync("dst_b", payload: null);
        Assert.Null(t.Last.Body);
        Assert.False(unresolved.Success);
        Assert.Null(unresolved.EventType);
        Assert.Equal("Field data.type is missing", unresolved.Error);

        // The original overload is unchanged: no body, and the HTTP result has no dry-run fields.
        var http = await client.Destinations.TestAsync("dst_h");
        Assert.Null(t.Last.Body);
        Assert.Null(http.DryRun);
        Assert.Null(http.MatchingEndpoints);
        Assert.Equal(200, http.Status);
    }

    [Fact]
    public async Task Create_returns_the_signing_secret_once_and_reads_hasSigningSecret()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"destination":{"id":"dst_1","name":"Orders","slug":"orders","url":"https://a.example/hook","method":"POST",
             "type":"http","isActive":true,"hasSigningSecret":true,"signingSecret":"whsec_abc"}}
            """, HttpStatusCode.Created)
         .Respond("""
            {"destination":{"id":"dst_2","name":"Queue","slug":"queue","url":"","type":"sqs",
             "isActive":true,"hasSigningSecret":false,"signingSecret":null}}
            """, HttpStatusCode.Created)
         .Respond("""{"destinations":[{"id":"dst_1","type":"http","hasSigningSecret":true}],"pagination":{"total":1,"page":1,"pageSize":20}}""");

        var http = await client.Destinations.CreateAsync(new CreateDestinationRequest { Name = "Orders", Url = "https://a.example/hook" });
        Assert.Equal("whsec_abc", http.SigningSecret);
        Assert.True(http.HasSigningSecret);

        var queue = await client.Destinations.CreateAsync(new CreateDestinationRequest { Name = "Queue", Type = DestinationType.Sqs });
        Assert.Null(queue.SigningSecret);
        Assert.False(queue.HasSigningSecret);

        var page = await client.Destinations.ListAsync();
        Assert.True(page.Data[0].HasSigningSecret);
        Assert.Null(page.Data[0].SigningSecret);
    }

    [Fact]
    public async Task Reveal_and_rotate_secret()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""{"signingSecret":"whsec_one","previousSecretExpiresAt":null}""")
         .Respond("""{"signingSecret":"whsec_two","previousSecretExpiresAt":"2026-11-12T11:00:00.000Z"}""")
         .Respond("""{"signingSecret":"whsec_three","previousSecretExpiresAt":null}""");

        var revealed = await client.Destinations.RevealSecretAsync("dst 1");
        Assert.Equal("GET /api/destinations/dst%201/reveal-secret", $"{t.Last.Method} {t.Last.Path}");
        Assert.Equal("whsec_one", revealed.SigningSecret);
        Assert.Null(revealed.PreviousSecretExpiresAt);

        var rotated = await client.Destinations.RotateSecretAsync("dst_1");
        Assert.Equal("POST /api/destinations/dst_1/rotate-secret", $"{t.Last.Method} {t.Last.Path}");
        Assert.Null(t.Last.Body);
        Assert.Equal("whsec_two", rotated.SigningSecret);
        Assert.Equal("2026-11-12T11:00:00.000Z", rotated.PreviousSecretExpiresAt);

        await client.Destinations.RotateSecretAsync("dst_1", gracePeriodSeconds: 0);
        Assert.Equal("""{"gracePeriodSeconds":0}""", t.Last.Body);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Destinations.RotateSecretAsync("dst_1", 86401));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Destinations.RotateSecretAsync("dst_1", -1));
        Assert.Equal(3, t.Requests.Count);
    }

    [Fact]
    public async Task An_outbound_message_published_by_a_bridge_says_where_it_came_from()
    {
        var (client, t) = TestTransport.Create();
        t.Respond("""
            {"data":{"id":"msg_1","eventId":"evt_o","status":"success","attempts":1,"maxAttempts":5,
             "bridge":{"inboundEventId":"evt_in","deliveryId":"dlv_1","hop":1},"holdExpiresAt":null}}
            """)
         .Respond("""{"data":{"id":"msg_2","status":"pending","attempts":0,"maxAttempts":5,"bridge":null}}""");

        var bridged = await client.Messages.GetAsync("msg_1");
        Assert.Equal(new Hookbase.Models.Messages.OutboundMessageBridge { InboundEventId = "evt_in", DeliveryId = "dlv_1", Hop = 1 }, bridged.Bridge);

        var direct = await client.Messages.GetAsync("msg_2");
        Assert.Null(direct.Bridge);
    }
}

using Hookbase.Json;
using Hookbase.Models.Endpoints;
using System.Text.Json;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Wire-format tests for POST /api/webhook-endpoints and PATCH /api/webhook-endpoints/:id.
/// These assert the exact bytes the SDK puts on the wire, because the API schemas reject any
/// key they do not declare.
/// </summary>
public class EndpointRequestSerializationTests
{
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, HookbaseJson.Options);

    private static JsonElement Parse<T>(T value)
        => JsonDocument.Parse(Serialize(value)).RootElement.Clone();

    [Fact]
    public void Create_SerializesEveryAcceptedFieldUnderItsApiName()
    {
        var request = new CreateEndpointRequest
        {
            ApplicationId = "app_123",
            Url = "https://customer.example.com/webhooks",
            Description = "Production webhook endpoint",
            Headers = new() { new EndpointHeader("X-Api-Key", "secret"), ("X-Tenant", "acme") },
            TimeoutSeconds = 30,
            RateLimitPerSecond = 100,
            SuccessStatusCodes = new() { 200, 201, "2xx" },
            BackoffType = BackoffType.Exponential,
            RetryDelays = new() { 1, 5, 30 },
            IpAllowlistNotes = "Customer allowlists 1.2.3.4",
            UseStaticIp = true,
            CircuitFailureThreshold = 5,
            CircuitSuccessThreshold = 2,
            CircuitCooldownSeconds = 60
        };

        Assert.Equal(
            "{\"applicationId\":\"app_123\"," +
            "\"url\":\"https://customer.example.com/webhooks\"," +
            "\"description\":\"Production webhook endpoint\"," +
            "\"headers\":[{\"name\":\"X-Api-Key\",\"value\":\"secret\"},{\"name\":\"X-Tenant\",\"value\":\"acme\"}]," +
            "\"timeoutSeconds\":30," +
            "\"rateLimitPerSecond\":100," +
            "\"successStatusCodes\":[200,201,\"2xx\"]," +
            "\"backoffType\":\"exponential\"," +
            "\"retryDelays\":[1,5,30]," +
            "\"ipAllowlistNotes\":\"Customer allowlists 1.2.3.4\"," +
            "\"useStaticIp\":true," +
            "\"circuitFailureThreshold\":5," +
            "\"circuitSuccessThreshold\":2," +
            "\"circuitCooldownSeconds\":60}",
            Serialize(request));
    }

    [Fact]
    public void Create_SendsNothingButTheRequiredKeysWhenNothingElseIsSet()
    {
        var request = new CreateEndpointRequest
        {
            ApplicationId = "app_123",
            Url = "https://customer.example.com/webhooks"
        };

        Assert.Equal(
            "{\"applicationId\":\"app_123\",\"url\":\"https://customer.example.com/webhooks\"}",
            Serialize(request));
    }

    [Theory]
    [InlineData("filterTypes")]
    [InlineData("rateLimit")]
    [InlineData("rateLimitPeriod")]
    [InlineData("metadata")]
    public void Create_NeverSerializesObsoleteMembers(string obsoleteKey)
    {
#pragma warning disable CS0618 // intentionally exercising the obsolete members
        var request = new CreateEndpointRequest
        {
            ApplicationId = "app_123",
            Url = "https://customer.example.com/webhooks",
            FilterTypes = new List<string> { "payment.*" },
            RateLimit = 100,
            RateLimitPeriod = 60,
            Metadata = new Dictionary<string, object> { ["team"] = "billing" }
        };
#pragma warning restore CS0618

        Assert.False(Parse(request).TryGetProperty(obsoleteKey, out _));
    }

    [Theory]
    [InlineData("filterTypes")]
    [InlineData("rateLimit")]
    [InlineData("rateLimitPeriod")]
    [InlineData("metadata")]
    public void Update_NeverSerializesObsoleteMembers(string obsoleteKey)
    {
#pragma warning disable CS0618 // intentionally exercising the obsolete members
        var request = new UpdateEndpointRequest
        {
            Url = "https://customer.example.com/webhooks",
            FilterTypes = new List<string> { "payment.*" },
            RateLimit = 100,
            RateLimitPeriod = 60,
            Metadata = new Dictionary<string, object> { ["team"] = "billing" }
        };
#pragma warning restore CS0618

        Assert.False(Parse(request).TryGetProperty(obsoleteKey, out _));
    }

    [Fact]
    public void Create_MapsLegacyRateLimitOntoRateLimitPerSecond()
    {
#pragma warning disable CS0618 // intentionally exercising the obsolete member
        var request = new CreateEndpointRequest
        {
            ApplicationId = "app_123",
            Url = "https://customer.example.com/webhooks",
            RateLimit = 100
        };
#pragma warning restore CS0618

        Assert.Equal(
            "{\"applicationId\":\"app_123\",\"url\":\"https://customer.example.com/webhooks\",\"rateLimitPerSecond\":100}",
            Serialize(request));
    }

    [Fact]
    public void Create_RateLimitPerSecondWinsWhenBothAreSet()
    {
#pragma warning disable CS0618 // intentionally exercising the obsolete member
        var request = new CreateEndpointRequest
        {
            ApplicationId = "app_123",
            Url = "https://customer.example.com/webhooks",
            RateLimit = 100,
            RateLimitPerSecond = 25
        };
#pragma warning restore CS0618

        Assert.Equal(25, Parse(request).GetProperty("rateLimitPerSecond").GetInt32());
    }

    [Fact]
    public void Update_MapsLegacyRateLimitOntoRateLimitPerSecond()
    {
#pragma warning disable CS0618 // intentionally exercising the obsolete member
        var legacyOnly = new UpdateEndpointRequest { RateLimit = 100 };
        var bothSet = new UpdateEndpointRequest { RateLimit = 100, RateLimitPerSecond = 25 };
#pragma warning restore CS0618

        Assert.Equal("{\"rateLimitPerSecond\":100}", Serialize(legacyOnly));
        Assert.Equal("{\"rateLimitPerSecond\":25}", Serialize(bothSet));
    }

    [Fact]
    public void Update_SerializesTheKeysOnlyPatchAccepts()
    {
        var request = new UpdateEndpointRequest
        {
            Url = "https://customer.example.com/v2",
            Description = "Moved",
            IsDisabled = true,
            DisabledReason = "Customer migrating",
            TimeoutSeconds = 10,
            RateLimitPerSecond = 5,
            SuccessStatusCodes = new() { "2xx" },
            BackoffType = BackoffType.Linear,
            RetryDelays = new() { 60 },
            IpAllowlistNotes = "n/a",
            UseStaticIp = false,
            CircuitFailureThreshold = 3,
            CircuitSuccessThreshold = 1,
            CircuitCooldownSeconds = 30,
            Headers = new() { ("X-Env", "prod") }
        };

        Assert.Equal(
            "{\"url\":\"https://customer.example.com/v2\"," +
            "\"description\":\"Moved\"," +
            "\"headers\":[{\"name\":\"X-Env\",\"value\":\"prod\"}]," +
            "\"timeoutSeconds\":10," +
            "\"rateLimitPerSecond\":5," +
            "\"successStatusCodes\":[\"2xx\"]," +
            "\"backoffType\":\"linear\"," +
            "\"retryDelays\":[60]," +
            "\"ipAllowlistNotes\":\"n/a\"," +
            "\"useStaticIp\":false," +
            "\"circuitFailureThreshold\":3," +
            "\"circuitSuccessThreshold\":1," +
            "\"circuitCooldownSeconds\":30," +
            "\"isDisabled\":true," +
            "\"disabledReason\":\"Customer migrating\"}",
            Serialize(request));
    }

    [Fact]
    public void Update_DoesNotSendApplicationId()
    {
        var request = new UpdateEndpointRequest { Url = "https://customer.example.com/v2" };

        Assert.False(Parse(request).TryGetProperty("applicationId", out _));
    }

    [Theory]
    [InlineData(BackoffType.Exponential, "exponential")]
    [InlineData(BackoffType.Linear, "linear")]
    [InlineData(BackoffType.Fixed, "fixed")]
    public void BackoffType_SerializesAsTheLowercaseWireValue(BackoffType backoffType, string expected)
    {
        var request = new UpdateEndpointRequest { BackoffType = backoffType };

        Assert.Equal(expected, Parse(request).GetProperty("backoffType").GetString());
    }

    [Fact]
    public void SuccessStatusCodes_RoundTripIntsAsNumbersAndPatternsAsStrings()
    {
        var codes = new List<SuccessStatusCode> { 200, "2xx" };

        var json = Serialize(codes);
        Assert.Equal("[200,\"2xx\"]", json);

        var roundTripped = JsonSerializer.Deserialize<List<SuccessStatusCode>>(json, HookbaseJson.Options);

        Assert.NotNull(roundTripped);
        Assert.Equal(2, roundTripped!.Count);

        Assert.Equal(200, roundTripped[0].Status);
        Assert.Null(roundTripped[0].Pattern);
        Assert.False(roundTripped[0].IsPattern);

        Assert.Equal("2xx", roundTripped[1].Pattern);
        Assert.Null(roundTripped[1].Status);
        Assert.True(roundTripped[1].IsPattern);

        Assert.Equal(codes, roundTripped);
    }

    [Fact]
    public void SuccessStatusCodes_RoundTripThroughACreateRequest()
    {
        var request = new CreateEndpointRequest
        {
            ApplicationId = "app_123",
            Url = "https://customer.example.com/webhooks",
            SuccessStatusCodes = new() { 202, "3xx" }
        };

        var json = Serialize(request);
        Assert.Contains("\"successStatusCodes\":[202,\"3xx\"]", json);

        var roundTripped = JsonSerializer.Deserialize<CreateEndpointRequest>(json, HookbaseJson.Options);

        Assert.NotNull(roundTripped);
        Assert.Equal(request.SuccessStatusCodes, roundTripped!.SuccessStatusCodes);
        Assert.Equal(json, Serialize(roundTripped));
    }

    [Fact]
    public void Headers_AlwaysSerializeAsNameValueObjects()
    {
        var request = new CreateEndpointRequest
        {
            ApplicationId = "app_123",
            Url = "https://customer.example.com/webhooks",
            Headers = new() { { "X-One", "1" } }
        };

        Assert.Equal(
            "[{\"name\":\"X-One\",\"value\":\"1\"}]",
            Parse(request).GetProperty("headers").GetRawText());
    }

    [Fact]
    public void Headers_AcceptTheLegacyUntypedListAndNormalizeIt()
    {
#pragma warning disable CS0618 // the legacy List<object> conversion is deliberately exercised
        var request = new CreateEndpointRequest
        {
            ApplicationId = "app_123",
            Url = "https://customer.example.com/webhooks",
            Headers = new List<object>
            {
                new EndpointHeader("X-One", "1"),
                new { name = "X-Two", value = "2" },
                new Dictionary<string, string> { ["name"] = "X-Three", ["value"] = "3" },
                new KeyValuePair<string, string>("X-Four", "4")
            }
        };
#pragma warning restore CS0618

        Assert.Equal(
            "[{\"name\":\"X-One\",\"value\":\"1\"}," +
            "{\"name\":\"X-Two\",\"value\":\"2\"}," +
            "{\"name\":\"X-Three\",\"value\":\"3\"}," +
            "{\"name\":\"X-Four\",\"value\":\"4\"}]",
            Parse(request).GetProperty("headers").GetRawText());
    }

    [Fact]
    public void Headers_DeserializeFromBothAnArrayAndAJsonEncodedString()
    {
        const string asArray = "{\"headers\":[{\"name\":\"X-One\",\"value\":\"1\"}]}";
        const string asString = "{\"headers\":\"[{\\\"name\\\":\\\"X-One\\\",\\\"value\\\":\\\"1\\\"}]\"}";

        var fromArray = JsonSerializer.Deserialize<Endpoint>(asArray, HookbaseJson.Options);
        var fromString = JsonSerializer.Deserialize<Endpoint>(asString, HookbaseJson.Options);

        Assert.Equal(new EndpointHeader("X-One", "1"), Assert.Single(fromArray!.Headers!));
        Assert.Equal(new EndpointHeader("X-One", "1"), Assert.Single(fromString!.Headers!));
    }
}

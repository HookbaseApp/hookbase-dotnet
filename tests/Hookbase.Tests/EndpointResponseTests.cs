using Hookbase.Json;
using Hookbase.Models.Endpoints;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// What an <see cref="Endpoint"/> holds after a response is deserialized, and how
/// <see cref="UpdateEndpointRequest.Clear"/> puts an explicit null on the wire.
/// </summary>
/// <remarks>
/// System.Text.Json drops a response key the record has no member for without a word, and
/// <c>UseStaticIp</c> defaulted to <c>true</c> where the API defaults it to false - so an endpoint
/// that did not use the static-IP proxy was reported as using it. Neither failure is visible to the
/// compiler; both are visible here.
/// </remarks>
public class EndpointResponseTests
{
    /// <summary>
    /// Every key the API's <c>formatEndpoint</c> returns
    /// (api/src/routes/webhook-endpoints.ts), with a value no CLR default would produce.
    /// </summary>
    private const string EndpointJson = """
    {
      "id": "ep_1",
      "applicationId": "app_1",
      "url": "https://customer.example.com/hooks",
      "description": "Primary",
      "secretPrefix": "whsec_abcdef...",
      "hasSecret": true,
      "secretVersion": 2,
      "headers": [{ "name": "X-Tenant", "value": "acme" }],
      "timeoutSeconds": 45,
      "isDisabled": false,
      "disabledAt": null,
      "disabledReason": null,
      "rateLimitPerSecond": 25,
      "successStatusCodes": [200, 201, "2xx"],
      "backoffType": "linear",
      "retryDelays": [5, 30, 300],
      "ipAllowlistNotes": "egress from 203.0.113.0/24",
      "useStaticIp": true,
      "circuitState": "closed",
      "circuitOpenedAt": null,
      "circuitFailureCount": 1,
      "circuitFailureThreshold": 7,
      "circuitSuccessThreshold": 3,
      "circuitCooldownSeconds": 120,
      "totalMessages": 900,
      "totalSuccesses": 880,
      "totalFailures": 20,
      "avgResponseTimeMs": 143.5,
      "lastSuccessAt": "2026-01-05T00:00:00Z",
      "lastFailureAt": "2026-01-04T00:00:00Z",
      "lastResponseStatus": 200,
      "isVerified": true,
      "verifiedAt": "2026-01-01T00:00:00Z",
      "createdAt": "2026-01-01T00:00:00Z",
      "updatedAt": "2026-01-06T00:00:00Z",
      "createdBy": "u1",
      "apiKeyId": null
    }
    """;

    private static Endpoint Deserialize(string json)
        => JsonSerializer.Deserialize<Endpoint>(json, HookbaseJson.Options)
           ?? throw new InvalidOperationException("the fixture did not deserialize");

    [Fact]
    public void Endpoint_HasAMemberForEveryResponseKey()
    {
        // The completeness guard. A key with no member is dropped silently, so add a key to
        // formatEndpoint, add it to the fixture above, and this fails until the record can hold it.
        var members = typeof(Endpoint)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = JsonDocument.Parse(EndpointJson).RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .Where(name => !members.Contains(name))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Endpoint_DeserializesTheDeliveryPolicy()
    {
        var endpoint = Deserialize(EndpointJson);

        Assert.Equal(25, endpoint.RateLimitPerSecond);
        Assert.Equal(new List<SuccessStatusCode> { 200, 201, "2xx" }, endpoint.SuccessStatusCodes);
        Assert.Equal(Models.Endpoints.BackoffType.Linear, endpoint.BackoffType);
        Assert.Equal(new List<int> { 5, 30, 300 }, endpoint.RetryDelays);
        Assert.Equal("egress from 203.0.113.0/24", endpoint.IpAllowlistNotes);
        Assert.True(endpoint.UseStaticIp);
    }

    [Fact]
    public void Endpoint_DeserializesTheRestOfTheResponse()
    {
        var endpoint = Deserialize(EndpointJson);

        Assert.Equal("whsec_abcdef...", endpoint.SecretPrefix);
        Assert.True(endpoint.HasSecret);
        Assert.Equal(2, endpoint.SecretVersion);
        Assert.Equal(45, endpoint.TimeoutSeconds);
        Assert.Equal(7, endpoint.CircuitFailureThreshold);
        Assert.Equal(120, endpoint.CircuitCooldownSeconds);
        Assert.Equal(143.5, endpoint.AvgResponseTimeMs);
        Assert.Equal(200, endpoint.LastResponseStatus);
        Assert.True(endpoint.IsVerified);
        Assert.Equal("u1", endpoint.CreatedBy);
        Assert.Null(endpoint.ApiKeyId);
    }

    [Fact]
    public void UseStaticIp_IsFalseWhenTheResponseDoesNotSayOtherwise()
    {
        // It defaulted to true, the opposite of the API's own default: sp_webhook_endpoint_create
        // stores `useStaticIp === true`, so an endpoint created without the setting has it off.
        var endpoint = Deserialize("""{ "id": "ep_1" }""");

        Assert.False(endpoint.UseStaticIp);
    }

    [Fact]
    public void Endpoint_KeepsNullPolicyFieldsNull()
    {
        // Null means "use the platform default", which a caller has to be able to tell apart from
        // a value. An empty list would erase the distinction.
        var endpoint = Deserialize("""
        {
          "id": "ep_1",
          "successStatusCodes": null,
          "retryDelays": null,
          "backoffType": null,
          "ipAllowlistNotes": null
        }
        """);

        Assert.Null(endpoint.SuccessStatusCodes);
        Assert.Null(endpoint.RetryDelays);
        Assert.Null(endpoint.BackoffType);
        Assert.Null(endpoint.IpAllowlistNotes);
    }

    // Clear: the only way to send an explicit null.
    //
    // Every member of UpdateEndpointRequest is nullable and a null one is omitted, so null means
    // "leave this alone" and there was nothing left to mean "reset this" - even though the API
    // accepts a null for five settings. A caller who had set a custom retry schedule could not
    // remove it.

    private static string Serialize(UpdateEndpointRequest request)
        => JsonSerializer.Serialize(request, HookbaseJson.Options);

    [Fact]
    public void Clear_SendsAnExplicitNull()
    {
        var json = Serialize(new UpdateEndpointRequest
        {
            Clear = { EndpointField.RetryDelays, EndpointField.BackoffType }
        });

        // Present AS null, not absent: an absent key leaves the setting alone.
        Assert.Equal("{\"retryDelays\":null,\"backoffType\":null}", json);
    }

    [Fact]
    public void Clear_LeavesTheRestOfThePatchAlone()
    {
        var json = Serialize(new UpdateEndpointRequest
        {
            Url = "https://customer.example.com/v2",
            Clear = { EndpointField.Description }
        });

        Assert.Equal(
            "{\"url\":\"https://customer.example.com/v2\",\"description\":null}",
            json);
    }

    [Fact]
    public void Clear_ThrowsWhenTheFieldIsAlsoSet()
    {
        // Set and cleared in one call is a contradiction. Picking one silently is how a caller
        // ends up with the opposite of what they wrote.
        var request = new UpdateEndpointRequest
        {
            Description = "still here",
            Clear = { EndpointField.Description }
        };

        var error = Assert.Throws<InvalidOperationException>(() => Serialize(request));
        Assert.Contains("Description", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Clear_ThrowsForAFieldTheApiWillNotNull()
    {
        // url and timeoutSeconds are `.optional()` only; a null for either is a 400. Saying which
        // field, here, beats the server's "Invalid input".
        var request = new UpdateEndpointRequest { Clear = { (EndpointField)99 } };

        var error = Assert.Throws<InvalidOperationException>(() => Serialize(request));
        Assert.Contains("does not accept", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutClear_TheBodyIsUnchanged()
    {
        // The clear path re-serializes through a node, so the no-clear path has to be shown to
        // still produce exactly what it did before - in particular, no null keys.
        Assert.Equal(
            "{\"timeoutSeconds\":45}",
            Serialize(new UpdateEndpointRequest { TimeoutSeconds = 45 }));
    }
}

using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Endpoints;

/// <summary>
/// Customer webhook endpoint.
/// </summary>
public record Endpoint
{
    public string? Id { get; init; }
    public string? ApplicationId { get; init; }
    public string? Url { get; init; }
    public string? Description { get; init; }
    public string? Secret { get; init; }
    public string? SecretPrefix { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool HasSecret { get; init; }

    public int? SecretVersion { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsDisabled { get; init; }

    public string? DisabledAt { get; init; }
    public string? DisabledReason { get; init; }
    public string? CircuitState { get; init; }
    public string? CircuitOpenedAt { get; init; }
    public int? CircuitFailureCount { get; init; }
    public int? CircuitFailureThreshold { get; init; }
    public int? CircuitSuccessThreshold { get; init; }
    public int? CircuitCooldownSeconds { get; init; }
    public List<string>? FilterTypes { get; init; }
    public int? RateLimit { get; init; }
    public int? RateLimitPeriod { get; init; }

    /// <summary>Maximum deliveries per second for this endpoint (0-1000).</summary>
    public int? RateLimitPerSecond { get; init; }

    public int? TimeoutSeconds { get; init; }
    public EndpointHeaderList? Headers { get; init; }

    /// <summary>Status codes (or patterns such as <c>"2xx"</c>) treated as a successful delivery.</summary>
    public List<SuccessStatusCode>? SuccessStatusCodes { get; init; }

    /// <summary>Retry backoff strategy.</summary>
    public BackoffType? BackoffType { get; init; }

    /// <summary>Explicit retry delays in seconds.</summary>
    public List<int>? RetryDelays { get; init; }

    /// <summary>Free-form notes about the customer's IP allowlist.</summary>
    public string? IpAllowlistNotes { get; init; }

    public Dictionary<string, object>? Metadata { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsVerified { get; init; }

    public string? VerifiedAt { get; init; }
    public int? SubscriptionCount { get; init; }
    public double? AvgResponseTimeMs { get; init; }
    public string? LastSuccessAt { get; init; }
    public string? LastFailureAt { get; init; }
    public int? LastResponseStatus { get; init; }
    [JsonConverter(typeof(BooleanConverter))]
    public bool UseStaticIp { get; init; } = true;

    public int TotalMessages { get; init; }
    public int TotalSuccesses { get; init; }
    public int TotalFailures { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Endpoint with full secret (returned on creation/rotation).
/// </summary>
public record EndpointWithSecret : Endpoint
{
    public new string? Secret { get; init; }
}

/// <summary>
/// Input for creating a new endpoint (<c>POST /api/webhook-endpoints</c>).
/// </summary>
/// <remarks>
/// Serialization goes through <see cref="CreateEndpointRequestConverter"/>, which emits only the
/// keys the API accepts. Members marked <see cref="ObsoleteAttribute"/> are never sent.
/// </remarks>
[JsonConverter(typeof(CreateEndpointRequestConverter))]
public record CreateEndpointRequest
{
    public required string ApplicationId { get; init; }
    public required string Url { get; init; }

    /// <summary>Human-readable description (max 500 characters).</summary>
    public string? Description { get; init; }

    /// <summary>Custom request headers, at most 10. Sent as <c>[{"name":...,"value":...}]</c>.</summary>
    public EndpointHeaderList? Headers { get; init; }

    /// <summary>Per-delivery timeout in seconds (1-120).</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>Maximum deliveries per second (0-1000).</summary>
    public int? RateLimitPerSecond { get; init; }

    /// <summary>
    /// Status codes treated as success, at most 20 entries. Each entry is either an exact code
    /// (<c>200</c>) or a wildcard string (<c>"2xx"</c>).
    /// </summary>
    public List<SuccessStatusCode>? SuccessStatusCodes { get; init; }

    /// <summary>Retry backoff strategy.</summary>
    public BackoffType? BackoffType { get; init; }

    /// <summary>Explicit retry delays in seconds, at most 10 entries, each 1-86400.</summary>
    public List<int>? RetryDelays { get; init; }

    /// <summary>Free-form notes about the customer's IP allowlist (max 1000 characters).</summary>
    public string? IpAllowlistNotes { get; init; }

    /// <summary>Deliver from Hookbase's static egress IPs.</summary>
    public bool? UseStaticIp { get; init; }

    /// <summary>Consecutive failures before the circuit opens (1-100).</summary>
    public int? CircuitFailureThreshold { get; init; }

    /// <summary>Consecutive successes before the circuit closes again (1-100).</summary>
    public int? CircuitSuccessThreshold { get; init; }

    /// <summary>Seconds the circuit stays open before a probe delivery (10-3600).</summary>
    public int? CircuitCooldownSeconds { get; init; }

    [Obsolete("filterTypes is not accepted by the Hookbase API and is never sent. " +
              "Use subscriptions (client.Subscriptions) to control which event types reach an endpoint.")]
    [JsonIgnore]
    public List<string>? FilterTypes { get; init; }

    [Obsolete("Renamed to RateLimitPerSecond. The value is still honored - it is sent as " +
              "rateLimitPerSecond when RateLimitPerSecond is not set - but the rateLimit key itself is never sent.")]
    [JsonIgnore]
    public int? RateLimit { get; init; }

    [Obsolete("rateLimitPeriod is not accepted by the Hookbase API and is never sent. " +
              "Endpoint rate limits are always per second; use RateLimitPerSecond.")]
    [JsonIgnore]
    public int? RateLimitPeriod { get; init; }

    [Obsolete("metadata is not accepted by the Hookbase API on webhook endpoints and is never sent.")]
    [JsonIgnore]
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Input for updating an existing endpoint (<c>PATCH /api/webhook-endpoints/:id</c>).
/// </summary>
/// <remarks>
/// Serialization goes through <see cref="UpdateEndpointRequestConverter"/>, which emits only the
/// keys the API accepts. Members marked <see cref="ObsoleteAttribute"/> are never sent.
/// </remarks>
[JsonConverter(typeof(UpdateEndpointRequestConverter))]
public record UpdateEndpointRequest
{
    public string? Url { get; init; }

    /// <summary>Human-readable description (max 500 characters).</summary>
    public string? Description { get; init; }

    /// <summary>Disable or re-enable the endpoint.</summary>
    public bool? IsDisabled { get; init; }

    /// <summary>Why the endpoint was disabled (max 500 characters).</summary>
    public string? DisabledReason { get; init; }

    /// <summary>Custom request headers, at most 10. Sent as <c>[{"name":...,"value":...}]</c>.</summary>
    public EndpointHeaderList? Headers { get; init; }

    /// <summary>Per-delivery timeout in seconds (1-120).</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>Maximum deliveries per second (0-1000).</summary>
    public int? RateLimitPerSecond { get; init; }

    /// <summary>
    /// Status codes treated as success, at most 20 entries. Each entry is either an exact code
    /// (<c>200</c>) or a wildcard string (<c>"2xx"</c>).
    /// </summary>
    public List<SuccessStatusCode>? SuccessStatusCodes { get; init; }

    /// <summary>Retry backoff strategy.</summary>
    public BackoffType? BackoffType { get; init; }

    /// <summary>Explicit retry delays in seconds, at most 10 entries, each 1-86400.</summary>
    public List<int>? RetryDelays { get; init; }

    /// <summary>Free-form notes about the customer's IP allowlist (max 1000 characters).</summary>
    public string? IpAllowlistNotes { get; init; }

    /// <summary>Deliver from Hookbase's static egress IPs.</summary>
    public bool? UseStaticIp { get; init; }

    /// <summary>Consecutive failures before the circuit opens (1-100).</summary>
    public int? CircuitFailureThreshold { get; init; }

    /// <summary>Consecutive successes before the circuit closes again (1-100).</summary>
    public int? CircuitSuccessThreshold { get; init; }

    /// <summary>Seconds the circuit stays open before a probe delivery (10-3600).</summary>
    public int? CircuitCooldownSeconds { get; init; }

    [Obsolete("filterTypes is not accepted by the Hookbase API and is never sent. " +
              "Use subscriptions (client.Subscriptions) to control which event types reach an endpoint.")]
    [JsonIgnore]
    public List<string>? FilterTypes { get; init; }

    [Obsolete("Renamed to RateLimitPerSecond. The value is still honored - it is sent as " +
              "rateLimitPerSecond when RateLimitPerSecond is not set - but the rateLimit key itself is never sent.")]
    [JsonIgnore]
    public int? RateLimit { get; init; }

    [Obsolete("rateLimitPeriod is not accepted by the Hookbase API and is never sent. " +
              "Endpoint rate limits are always per second; use RateLimitPerSecond.")]
    [JsonIgnore]
    public int? RateLimitPeriod { get; init; }

    [Obsolete("metadata is not accepted by the Hookbase API on webhook endpoints and is never sent.")]
    [JsonIgnore]
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Result of rotating an endpoint's signing secret.
/// </summary>
public record RotateSecretResult
{
    public string? Secret { get; init; }
    public string? PreviousSecretExpiresAt { get; init; }
    public int? SecretVersion { get; init; }
}

/// <summary>
/// Endpoint statistics.
/// </summary>
public record EndpointStats
{
    public int TotalMessages { get; init; }
    public int TotalSuccesses { get; init; }
    public int TotalFailures { get; init; }
    public double SuccessRate { get; init; }
    public double AverageLatency { get; init; }
    public int RecentFailures { get; init; }
}

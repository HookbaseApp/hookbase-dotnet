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
    public int? TimeoutSeconds { get; init; }
    public List<object>? Headers { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsVerified { get; init; }

    public string? VerifiedAt { get; init; }
    public int? SubscriptionCount { get; init; }
    public double? AvgResponseTimeMs { get; init; }
    public string? LastSuccessAt { get; init; }
    public string? LastFailureAt { get; init; }
    public int? LastResponseStatus { get; init; }
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
/// Input for creating a new endpoint.
/// </summary>
public record CreateEndpointRequest
{
    public required string ApplicationId { get; init; }
    public required string Url { get; init; }
    public string? Description { get; init; }
    public List<string>? FilterTypes { get; init; }
    public int? RateLimit { get; init; }
    public int? RateLimitPeriod { get; init; }
    public int? TimeoutSeconds { get; init; }
    public List<object>? Headers { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Input for updating an existing endpoint.
/// </summary>
public record UpdateEndpointRequest
{
    public string? Url { get; init; }
    public string? Description { get; init; }
    public bool? IsDisabled { get; init; }
    public List<string>? FilterTypes { get; init; }
    public int? RateLimit { get; init; }
    public int? RateLimitPeriod { get; init; }
    public int? TimeoutSeconds { get; init; }
    public List<object>? Headers { get; init; }
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

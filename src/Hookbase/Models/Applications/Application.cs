using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Applications;

/// <summary>
/// Customer webhook application.
/// </summary>
public record Application
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string OrganizationId { get; init; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    public Dictionary<string, object>? Metadata { get; init; }

    // Rate limits
    public int? RateLimitPerSecond { get; init; }
    public int? RateLimitPerMinute { get; init; }
    public int? RateLimitPerHour { get; init; }

    // Status
    [JsonConverter(typeof(BooleanConverter))]
    public bool IsDisabled { get; init; }
    public DateTime? DisabledAt { get; init; }
    public string? DisabledReason { get; init; }

    // Statistics
    public int? TotalEndpoints { get; init; }
    public int? TotalMessagesSent { get; init; }
    public int? TotalMessagesFailed { get; init; }
    public DateTime? LastEventAt { get; init; }
    public int? EndpointCount { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Input for creating a new application.
/// </summary>
public record CreateApplicationRequest
{
    public required string Name { get; init; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    public Dictionary<string, object>? Metadata { get; init; }
    public int? RateLimitPerSecond { get; init; }
    public int? RateLimitPerMinute { get; init; }
    public int? RateLimitPerHour { get; init; }
}

/// <summary>
/// Input for updating an existing application.
/// </summary>
public record UpdateApplicationRequest
{
    public string? Name { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    public int? RateLimitPerSecond { get; init; }
    public int? RateLimitPerMinute { get; init; }
    public int? RateLimitPerHour { get; init; }
    public bool? IsDisabled { get; init; }
    public string? DisabledReason { get; init; }
}

/// <summary>
/// Input for get-or-create operation.
/// </summary>
public record GetOrCreateApplicationRequest
{
    [JsonPropertyName("externalId")]
    public required string ExternalId { get; init; }

    public required string Name { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    public int? RateLimitPerSecond { get; init; }
    public int? RateLimitPerMinute { get; init; }
    public int? RateLimitPerHour { get; init; }
}

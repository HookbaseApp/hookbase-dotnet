using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Destinations;

/// <summary>
/// Webhook delivery destination.
/// </summary>
public record Destination
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
    [JsonConverter(typeof(JsonStringStringDictionaryConverter))]
    public Dictionary<string, string>? Headers { get; init; }
    public string? AuthType { get; init; }
    [JsonConverter(typeof(JsonStringDictionaryConverter))]
    public Dictionary<string, object>? AuthConfig { get; init; }
    public int TimeoutMs { get; init; } = 30000;
    public int? RateLimitPerMinute { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsActive { get; init; } = true;

    [JsonConverter(typeof(BooleanConverter))]
    public bool MockEnabled { get; init; }

    [JsonConverter(typeof(JsonStringDictionaryConverter))]
    public Dictionary<string, object>? MockConfig { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }

    // List endpoint only - computed fields
    public int? RouteCount { get; init; }
    public int? SuccessCount { get; init; }
    public int? FailureCount { get; init; }
}

/// <summary>
/// Input for creating a new destination.
/// </summary>
public record CreateDestinationRequest
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string Url { get; init; }
    public string? Method { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? AuthType { get; init; }
    public Dictionary<string, object>? AuthConfig { get; init; }
    public int? TimeoutMs { get; init; }
    public int? RateLimitPerMinute { get; init; }
}

/// <summary>
/// Input for updating an existing destination.
/// </summary>
public record UpdateDestinationRequest
{
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? AuthType { get; init; }
    public Dictionary<string, object>? AuthConfig { get; init; }
    public int? TimeoutMs { get; init; }
    public int? RateLimitPerMinute { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// Result of testing a destination connection.
/// </summary>
public record TestDestinationResult
{
    public bool Success { get; init; }
    public int? Status { get; init; }
    public int? LatencyMs { get; init; }
    public string? ResponseBody { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Export result containing destinations with metadata.
/// </summary>
public record DestinationExport
{
    public string? Version { get; init; }
    public string? ExportedAt { get; init; }
    public string? OrganizationSlug { get; init; }
    public List<Destination>? Destinations { get; init; }
}

/// <summary>
/// Request for importing destinations.
/// </summary>
public record ImportDestinationsRequest
{
    public required List<CreateDestinationRequest> Destinations { get; init; }
    public string ConflictStrategy { get; init; } = "skip"; // skip, rename, overwrite
    public bool? ValidateOnly { get; init; }
}

/// <summary>
/// Result of importing destinations.
/// </summary>
public record ImportDestinationsResult
{
    public bool Success { get; init; }
    public ImportSummary? Summary { get; init; }
    public ImportDetails? Details { get; init; }
}

public record ImportSummary
{
    public int Imported { get; init; }
    public int Skipped { get; init; }
    public int Overwritten { get; init; }
    public int Failed { get; init; }
}

public record ImportDetails
{
    public List<string>? Imported { get; init; }
    public List<string>? Skipped { get; init; }
    public List<string>? Overwritten { get; init; }
    public List<Dictionary<string, string>>? Failed { get; init; }
}

/// <summary>
/// Request for bulk delete operations.
/// </summary>
public record BulkDeleteRequest
{
    public required List<string> Ids { get; init; }
}

/// <summary>
/// Result of bulk delete operation.
/// </summary>
public record BulkDeleteResult
{
    public bool Success { get; init; }
    public int Deleted { get; init; }
}

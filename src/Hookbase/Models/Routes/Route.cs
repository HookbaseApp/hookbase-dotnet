using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Routes;

public record FilterCondition
{
    public required string Field { get; init; }
    public string? Operator { get; init; }
    public required object Value { get; init; }
}

/// <summary>
/// Webhook routing rule.
/// </summary>
public record Route
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? SourceId { get; init; }
    public string? DestinationId { get; init; }
    public string? FilterId { get; init; }
    public List<FilterCondition>? FilterConditions { get; init; }
    public string? FilterLogic { get; init; }
    public string? TransformId { get; init; }
    public string? SchemaId { get; init; }
    public int Priority { get; init; } = 100;

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsActive { get; init; } = true;

    public string? CircuitState { get; init; }
    public string? CircuitOpenedAt { get; init; }
    public int? CircuitCooldownSeconds { get; init; }
    public int? CircuitFailureThreshold { get; init; }
    public int? CircuitProbeSuccessThreshold { get; init; }
    public int? CircuitProbeAttempts { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool NotifyOnFailure { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool NotifyOnSuccess { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool NotifyOnRecovery { get; init; }

    public string? NotifyEmails { get; init; }
    public int? FailureThreshold { get; init; }
    public int? ConsecutiveFailures { get; init; }
    public string? LastNotificationAt { get; init; }
    public List<string>? FailoverDestinationIds { get; init; }
    public int? FailoverAfterAttempts { get; init; }

    // Joined fields from list/get
    public string? SourceName { get; init; }
    public string? SourceSlug { get; init; }
    public string? DestinationName { get; init; }
    public string? DestinationUrl { get; init; }
    public string? TransformName { get; init; }
    public string? FilterName { get; init; }
    public string? SchemaName { get; init; }

    public int EventCount { get; init; }
    public int DeliveryCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public string? LastEventAt { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Input for creating a new route.
/// </summary>
public record CreateRouteRequest
{
    public required string Name { get; init; }
    public required string SourceId { get; init; }
    public required string DestinationId { get; init; }
    public string? FilterId { get; init; }
    public List<FilterCondition>? FilterConditions { get; init; }
    public string? FilterLogic { get; init; }
    public string? TransformId { get; init; }
    public string? SchemaId { get; init; }
    public int? Priority { get; init; }
    public int? CircuitCooldownSeconds { get; init; }
    public int? CircuitFailureThreshold { get; init; }
    public int? CircuitProbeSuccessThreshold { get; init; }
    public bool? NotifyOnFailure { get; init; }
    public bool? NotifyOnSuccess { get; init; }
    public bool? NotifyOnRecovery { get; init; }
}

/// <summary>
/// Input for updating an existing route.
/// </summary>
public record UpdateRouteRequest
{
    public string? Name { get; init; }
    public string? DestinationId { get; init; }
    public string? FilterId { get; init; }
    public List<FilterCondition>? FilterConditions { get; init; }
    public string? FilterLogic { get; init; }
    public string? TransformId { get; init; }
    public string? SchemaId { get; init; }
    public int? Priority { get; init; }
    public bool? IsActive { get; init; }
    public int? CircuitCooldownSeconds { get; init; }
    public int? CircuitFailureThreshold { get; init; }
    public int? CircuitProbeSuccessThreshold { get; init; }
    public bool? NotifyOnFailure { get; init; }
    public bool? NotifyOnSuccess { get; init; }
    public bool? NotifyOnRecovery { get; init; }
}

/// <summary>
/// Circuit breaker status for a route.
/// </summary>
public record CircuitBreakerStatus
{
    public string State { get; init; } = "closed";
    public string? OpenedAt { get; init; }
    public int FailureCount { get; init; }
    public int SuccessCount { get; init; }
}

/// <summary>
/// Bulk delete routes request.
/// </summary>
public record BulkDeleteRoutesRequest
{
    public required List<string> RouteIds { get; init; }
}

/// <summary>
/// Bulk delete result.
/// </summary>
public record BulkDeleteResult
{
    public int Deleted { get; init; }
    public List<string>? Failed { get; init; }
}

/// <summary>
/// Bulk update routes request.
/// </summary>
public record BulkUpdateRoutesRequest
{
    public required List<string> RouteIds { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>
/// Bulk update result.
/// </summary>
public record BulkUpdateResult
{
    public int Updated { get; init; }
    public List<string>? Failed { get; init; }
}

/// <summary>
/// Route export format.
/// </summary>
public record RouteExport
{
    public string? Version { get; init; }
    public string? ExportedAt { get; init; }
    public string? OrganizationSlug { get; init; }
    public List<ExportedRoute>? Routes { get; init; }
}

/// <summary>
/// Exported route.
/// </summary>
public record ExportedRoute
{
    public string? Name { get; init; }
    public string? SourceSlug { get; init; }
    public string? DestinationSlug { get; init; }
    public string? FilterName { get; init; }
    public string? TransformName { get; init; }
    public string? SchemaName { get; init; }
    public int Priority { get; init; }
    public bool IsActive { get; init; }
    public bool NotifyOnFailure { get; init; }
    public bool NotifyOnSuccess { get; init; }
    public bool NotifyOnRecovery { get; init; }
    public string? NotifyEmails { get; init; }
    public int FailureThreshold { get; init; }
    public List<string>? FailoverDestinationSlugs { get; init; }
    public int FailoverAfterAttempts { get; init; }
    public int CircuitCooldownSeconds { get; init; }
    public int CircuitFailureThreshold { get; init; }
    public int CircuitProbeSuccessThreshold { get; init; }
}

/// <summary>
/// Import routes request.
/// </summary>
public record ImportRoutesRequest
{
    public required List<ExportedRoute> Routes { get; init; }
    public required string ConflictStrategy { get; init; } // "skip", "rename", "overwrite"
    public bool? ValidateOnly { get; init; }
}

/// <summary>
/// Import routes result.
/// </summary>
public record ImportRoutesResult
{
    public bool Success { get; init; }
    public List<string>? Imported { get; init; }
    public List<string>? Skipped { get; init; }
    public List<ImportError>? Errors { get; init; }
    public ImportSummary? Summary { get; init; }
}

public record ImportError
{
    public required string Name { get; init; }
    public required string Error { get; init; }
}

public record ImportSummary
{
    public int Total { get; init; }
    public int Imported { get; init; }
    public int Skipped { get; init; }
    public int Errors { get; init; }
}

/// <summary>
/// Circuit breaker status response.
/// </summary>
public record CircuitStatusResponse
{
    public string? CircuitState { get; init; }
    public string? CircuitOpenedAt { get; init; }
    public int CooldownSeconds { get; init; }
    public int ProbeAttempts { get; init; }
    public int ProbeSuccessThreshold { get; init; }
    public int FailureThreshold { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int? TimeUntilProbeSeconds { get; init; }
}

/// <summary>
/// Circuit breaker reset result.
/// </summary>
public record CircuitResetResult
{
    public bool Success { get; init; }
    public string? CircuitState { get; init; }
    public string? PreviousState { get; init; }
}

/// <summary>
/// Update circuit config request.
/// </summary>
public record UpdateCircuitConfigRequest
{
    public int? CircuitCooldownSeconds { get; init; }
    public int? CircuitFailureThreshold { get; init; }
    public int? CircuitProbeSuccessThreshold { get; init; }
}

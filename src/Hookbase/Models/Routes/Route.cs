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
    public string? FilterSlug { get; init; }
    public string? TransformSlug { get; init; }
    public string? SchemaSlug { get; init; }
    public List<FilterCondition>? FilterConditions { get; init; }
    public string? FilterLogic { get; init; }
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

    /// <summary>Same as <see cref="FilterSlug"/>; a value set here is sent as <c>filterSlug</c>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is filterSlug. Use FilterSlug.")]
    public string? FilterName { get => FilterSlug; init => FilterSlug = value; }

    /// <summary>Same as <see cref="TransformSlug"/>; a value set here is sent as <c>transformSlug</c>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is transformSlug. Use TransformSlug.")]
    public string? TransformName { get => TransformSlug; init => TransformSlug = value; }

    /// <summary>Same as <see cref="SchemaSlug"/>; a value set here is sent as <c>schemaSlug</c>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is schemaSlug. Use SchemaSlug.")]
    public string? SchemaName { get => SchemaSlug; init => SchemaSlug = value; }
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
/// <remarks>
/// Applied: <c>Success</c>, <c>Summary</c> (<c>Total</c>, <c>Imported</c> = created, <c>Skipped</c>,
/// <c>Overwritten</c>, <c>Failed</c>) and <c>Details</c>, the same shape as a source or destination
/// import. The top-level <c>Imported</c> / <c>Skipped</c> / <c>Errors</c> lists are the older
/// routes-only shape; there <c>Imported</c> lists created and overwritten routes together.
/// With <c>ValidateOnly</c>: <c>Valid</c>, <c>ValidationResults</c> and <c>Summary</c>
/// (<c>Total</c>, <c>ToCreate</c>, <c>ToOverwrite</c>, <c>ToSkip</c>, <c>Errors</c>).
/// An API older than the unified shape sends no <c>Overwritten</c>, counting overwrites in
/// <c>Summary.Imported</c>.
/// </remarks>
public record ImportRoutesResult
{
    public bool Success { get; init; }
    public bool ValidateOnly { get; init; }

    /// <summary>Validate-only: whether no route has status <c>error</c>; null on an applied import.</summary>
    public bool? Valid { get; init; }

    public List<Hookbase.Models.Common.ImportValidationResult>? ValidationResults { get; init; }
    public List<string>? Imported { get; init; }
    public List<string>? Skipped { get; init; }
    public List<ImportError>? Errors { get; init; }
    public ImportSummary? Summary { get; init; }

    /// <summary>Applied: which routes landed where, by name.</summary>
    public Hookbase.Models.Destinations.ImportDetails? Details { get; init; }
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

    /// <summary>Applied: routes overwritten (not counted in <c>Imported</c>).</summary>
    public int? Overwritten { get; init; }

    /// <summary>Applied: routes that failed; same as <c>Errors</c>.</summary>
    public int? Failed { get; init; }

    /// <summary>Validate-only: routes that would be created.</summary>
    public int? ToCreate { get; init; }

    /// <summary>Validate-only: routes that would be overwritten.</summary>
    public int? ToOverwrite { get; init; }

    /// <summary>Validate-only: routes that would be skipped.</summary>
    public int? ToSkip { get; init; }

    /// <summary>Validate-only: routes that would be created or renamed. Older name; prefer <c>ToCreate</c>.</summary>
    public int? Valid { get; init; }

    /// <summary>Validate-only: routes that would be overwritten. Older name; prefer <c>ToOverwrite</c>.</summary>
    public int? Overwrite { get; init; }
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

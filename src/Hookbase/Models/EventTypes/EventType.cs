using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.EventTypes;

/// <summary>
/// Event type definition for outbound webhooks.
/// </summary>
public record EventType
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public object? Schema { get; init; }
    public int? SchemaVersion { get; init; }
    public string? ExamplePayload { get; init; }
    public string? DocumentationUrl { get; init; }
    public int? DefaultPriority { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsEnabled { get; init; } = true;

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsDeprecated { get; init; }

    public string? DeprecatedAt { get; init; }
    public string? DeprecatedMessage { get; init; }
    public int? SubscriptionCount { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Input for creating a new event type.
/// </summary>
public record CreateEventTypeRequest
{
    public required string Name { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Schema { get; init; }
}

/// <summary>
/// Input for updating an existing event type.
/// </summary>
public record UpdateEventTypeRequest
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Schema { get; init; }
    public bool? IsEnabled { get; init; }
}

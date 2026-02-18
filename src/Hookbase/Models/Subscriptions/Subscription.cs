using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Subscriptions;

/// <summary>
/// Webhook subscription linking endpoint to event type.
/// </summary>
public record Subscription
{
    public string? Id { get; init; }
    public string? EndpointId { get; init; }
    public string? EventTypeId { get; init; }
    public string? EventTypeName { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsEnabled { get; init; } = true;

    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Input for creating a new subscription.
/// </summary>
public record CreateSubscriptionRequest
{
    public required string EndpointId { get; init; }
    public required string EventTypeId { get; init; }
}

/// <summary>
/// Input for updating an existing subscription.
/// </summary>
public record UpdateSubscriptionRequest
{
    public bool? IsEnabled { get; init; }
}

/// <summary>
/// Input for subscribing to multiple event types at once.
/// </summary>
public record SubscribeToManyRequest
{
    public required string EndpointId { get; init; }
    public required List<string> EventTypeIds { get; init; }
}

/// <summary>
/// Input for bulk subscribe operation.
/// </summary>
public record BulkSubscribeRequest
{
    public required string EndpointId { get; init; }
    public required List<string> EventTypeIds { get; init; }
    public Dictionary<string, string>? LabelFilters { get; init; }
    public string? LabelFilterMode { get; init; }
}

/// <summary>
/// Result of bulk subscribe operation.
/// </summary>
public record BulkSubscribeResult
{
    public int Created { get; init; }
    public int Skipped { get; init; }
    public List<Subscription>? Subscriptions { get; init; }
}

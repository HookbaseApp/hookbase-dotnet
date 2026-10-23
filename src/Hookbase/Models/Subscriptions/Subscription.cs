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

    /// <summary>The event type's name. Returned by the list; <c>GET /:id</c> nests it under <see cref="EventType"/>.</summary>
    public string? EventTypeName { get; init; }

    /// <summary>List only.</summary>
    public string? EventTypeDisplayName { get; init; }

    /// <summary>List only.</summary>
    public string? EndpointUrl { get; init; }

    /// <summary>List only.</summary>
    public string? ApplicationId { get; init; }

    /// <summary>List only.</summary>
    public string? ApplicationName { get; init; }

    /// <summary>JSONata expression evaluated on the payload; the event is delivered only when it is truthy.</summary>
    public string? FilterExpression { get; init; }

    /// <summary>Label filters: each key maps to a value or an array of values.</summary>
    public System.Text.Json.JsonElement? LabelFilters { get; init; }

    /// <summary><c>all</c> or <c>any</c>.</summary>
    public string? LabelFilterMode { get; init; }

    public string? TransformId { get; init; }

    /// <summary>Priority override: 0=Critical, 1=High, 2=Normal, 3=Low, 4=Bulk; null uses the event type's.</summary>
    public int? PriorityOverride { get; init; }

    /// <summary>The pinned event-type version label, or <c>latest</c>.</summary>
    public string? EventVersion { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool EventVersionDeprecated { get; init; }

    public string? EventVersionSunsetAt { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsEnabled { get; init; } = true;

    public string? CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public string? ApiKeyId { get; init; }

    /// <summary>The event type, as <c>GET /:id</c> returns it (null from the list).</summary>
    public SubscriptionEventTypeRef? EventType { get; init; }

    /// <summary>The endpoint, as <c>GET /:id</c> returns it (null from the list).</summary>
    public System.Text.Json.JsonElement? Endpoint { get; init; }

    /// <summary>The application, as <c>GET /:id</c> returns it (null from the list).</summary>
    public System.Text.Json.JsonElement? Application { get; init; }

    [Obsolete("The API does not return updatedAt for subscriptions; it is always null.")]
    public string? UpdatedAt { get; init; }
}

/// <summary>The event type nested in a subscription from <c>GET /webhook-subscriptions/:id</c>.</summary>
public record SubscriptionEventTypeRef
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
}

/// <summary>
/// Input for creating a new subscription.
/// </summary>
public record CreateSubscriptionRequest
{
    public required string EndpointId { get; init; }
    public required string EventTypeId { get; init; }

    /// <summary>JSONata expression (max 1000 chars); the event is delivered only when it is truthy.</summary>
    public string? FilterExpression { get; init; }

    /// <summary>Label filters: each value is a string or a <c>string[]</c> (max 50 keys).</summary>
    public Dictionary<string, object>? LabelFilters { get; init; }

    /// <summary><c>all</c> (API default) or <c>any</c>.</summary>
    public string? LabelFilterMode { get; init; }

    /// <summary>Transform (UUID) applied to the payload for this subscription.</summary>
    public string? TransformId { get; init; }

    /// <summary>Defaults to true on the API.</summary>
    public bool? IsEnabled { get; init; }

    /// <summary>Priority override: 0=Critical, 1=High, 2=Normal, 3=Low, 4=Bulk.</summary>
    public int? PriorityOverride { get; init; }

    /// <summary><c>latest</c> or a version label. Unset pins the version that is latest now.</summary>
    public string? EventVersion { get; init; }
}

/// <summary>
/// Input for updating an existing subscription. Unset (null) properties are left unchanged.
/// </summary>
public record UpdateSubscriptionRequest
{
    public bool? IsEnabled { get; init; }
    public string? FilterExpression { get; init; }

    /// <summary>Label filters: each value is a string or a <c>string[]</c>.</summary>
    public Dictionary<string, object>? LabelFilters { get; init; }

    /// <summary><c>all</c> or <c>any</c>.</summary>
    public string? LabelFilterMode { get; init; }

    public string? TransformId { get; init; }
    public int? PriorityOverride { get; init; }

    /// <summary><c>latest</c> (follow the newest version) or a version label (pin).</summary>
    public string? EventVersion { get; init; }
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

    /// <summary>Filter expression applied to every created subscription.</summary>
    public string? FilterExpression { get; init; }

    /// <summary>Transform (UUID) applied to every created subscription.</summary>
    public string? TransformId { get; init; }

    /// <summary>Only <c>latest</c> is accepted here; omitted pins each to its event type's current version.</summary>
    public string? EventVersion { get; init; }
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

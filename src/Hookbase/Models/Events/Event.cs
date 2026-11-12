using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Events;

public record DeliveryStats
{
    public int Total { get; init; }
    public int Delivered { get; init; }
    public int Failed { get; init; }
    public int Pending { get; init; }
}

/// <summary>
/// Inbound webhook event.
/// </summary>
public record InboundEvent
{
    public string? Id { get; init; }
    public string? SourceId { get; init; }
    public string? OrganizationId { get; init; }
    public string? EventType { get; init; }
    public string? PayloadHash { get; init; }
    public object? SignatureValid { get; init; }
    public string? ReceivedAt { get; init; }
    public string? IpAddress { get; init; }
    public string? SourceName { get; init; }
    public string? SourceSlug { get; init; }
    public string? Status { get; init; }

    /// <summary>The HTTP verb the webhook arrived with.</summary>
    public string? Method { get; init; }

    public DeliveryStats? DeliveryStats { get; init; }
}

/// <summary>
/// Detailed event information including payload and deliveries (<c>GET /api/events/{id}</c>).
/// </summary>
/// <remarks>
/// The API answers <c>{ event, payload, transient, deliveries }</c>; the SDK folds the payload,
/// the transient flag and the deliveries into this one object.
/// </remarks>
public record EventDetail
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? SourceId { get; init; }
    public string? EventType { get; init; }
    public string? PayloadHash { get; init; }
    public string? PayloadKey { get; init; }

    /// <summary>The stored payload; null once it has expired, or for a transient event.</summary>
    public object? Payload { get; init; }

    /// <summary>
    /// The source does not store payloads, so none can be shown or replayed (JSON <c>transient</c>).
    /// </summary>
    public bool Transient { get; init; }

    [JsonConverter(typeof(JsonStringStringDictionaryConverter))]
    public Dictionary<string, string>? Headers { get; init; }
    public object? SignatureValid { get; init; }
    public string? ReceivedAt { get; init; }
    public string? IpAddress { get; init; }
    public string? SourceName { get; init; }

    /// <summary>As on <see cref="InboundEvent.Status"/>.</summary>
    public string? Status { get; init; }

    /// <summary>The HTTP verb the webhook arrived with.</summary>
    public string? Method { get; init; }

    public object? EncryptFields { get; init; }
    public object? MaskFields { get; init; }
    public List<EventDeliveryInfo>? Deliveries { get; init; }
}

/// <summary>
/// A delivery of an event, as listed on <see cref="EventDetail.Deliveries"/>. Same fields as
/// <see cref="Hookbase.Models.Deliveries.Delivery"/>.
/// </summary>
public record EventDeliveryInfo : Hookbase.Models.Deliveries.Delivery
{
}

/// <summary>
/// Debug information for an event including curl command.
/// </summary>
public record EventDebugInfo
{
    /// <summary>
    /// The event. <see cref="EventDebugData.Headers"/> and <see cref="EventDebugData.Payload"/> are
    /// filled from <see cref="Request"/> and <see cref="Payload"/>, where the API sends them.
    /// </summary>
    public EventDebugData? Event { get; init; }

    /// <summary>The request as received: headers, payload size and content type.</summary>
    public EventDebugRequest? Request { get; init; }

    /// <summary>The parsed payload (null when it was not JSON or has expired).</summary>
    public object? Payload { get; init; }

    /// <summary>The raw body as text, cut at 10,000 characters.</summary>
    public string? RawPayload { get; init; }

    /// <summary>True when the payload is held only transiently (no stored copy to replay).</summary>
    public bool? Transient { get; init; }

    /// <summary>
    /// Deliveries with transform output, circuit state and failure hints, as raw JSON. Read them
    /// typed with <see cref="GetDeliveries"/>.
    /// </summary>
    public System.Text.Json.JsonElement? Deliveries { get; init; }

    /// <summary>
    /// <see cref="Deliveries"/> as <see cref="EventDebugDelivery"/> records; empty when there are
    /// none. Parsed on each call.
    /// </summary>
    public IReadOnlyList<EventDebugDelivery> GetDeliveries()
    {
        if (Deliveries is not { ValueKind: System.Text.Json.JsonValueKind.Array } deliveries)
            return Array.Empty<EventDebugDelivery>();

        return System.Text.Json.JsonSerializer.Deserialize<List<EventDebugDelivery>>(deliveries, HookbaseJson.Options)
            ?? new List<EventDebugDelivery>();
    }

    public System.Text.Json.JsonElement? Timing { get; init; }
    public System.Text.Json.JsonElement? FilterEvaluations { get; init; }
    public System.Text.Json.JsonElement? RecentConfigChanges { get; init; }

    /// <summary>Holds <c>curlCommand</c>.</summary>
    public EventDebugExtras? Debug { get; init; }

    private readonly string? _curlCommand;

    /// <summary>
    /// A curl command that replays the request against the ingest URL (null for transient events).
    /// The API nests it under <c>debug</c>; this reads it from there.
    /// </summary>
    public string? CurlCommand
    {
        get => _curlCommand ?? Debug?.CurlCommand;
        init => _curlCommand = value;
    }
}

/// <summary>
/// One delivery on the event debugger (<c>GET /api/events/:id/debug</c>, <c>deliveries[]</c>), read
/// with <see cref="EventDebugInfo.GetDeliveries"/>. The names follow the debug view, not
/// <c>Delivery</c>: <see cref="StatusCode"/>, <see cref="DurationMs"/>, <see cref="Attempt"/>,
/// <see cref="Error"/>.
/// </summary>
public record EventDebugDelivery
{
    public string? Id { get; init; }

    /// <summary>e.g. <c>pending</c>, <c>delivered</c>, <c>failed</c>, <c>awaiting_retry</c>, <c>held</c>.</summary>
    public string? Status { get; init; }

    /// <summary>The destination's HTTP status; null when no response came back.</summary>
    public int? StatusCode { get; init; }

    public int? DurationMs { get; init; }

    /// <summary>Attempts made so far.</summary>
    public int? Attempt { get; init; }

    public int? MaxAttempts { get; init; }

    /// <summary>When the next attempt is due; null when none is scheduled or the delivery is <c>held</c>.</summary>
    public string? NextRetryAt { get; init; }

    /// <summary>For a <c>held</c> delivery, when it is dead-lettered if still held; null otherwise.</summary>
    public string? HoldExpiresAt { get; init; }

    public string? Error { get; init; }
    public string? ResponseBody { get; init; }

    [JsonConverter(typeof(JsonStringStringDictionaryConverter))]
    public Dictionary<string, string>? ResponseHeaders { get; init; }

    /// <summary>The payload after the route's transform; null without a transform or once expired.</summary>
    public System.Text.Json.JsonElement? TransformedPayload { get; init; }

    public string? RouteId { get; init; }
    public string? DestinationId { get; init; }
    public EventDebugDestination? Destination { get; init; }
    public string? StartedAt { get; init; }
    public string? CompletedAt { get; init; }

    /// <summary>0 for the primary destination, 1 and up for failover destinations.</summary>
    public int FailoverIndex { get; init; }

    /// <summary>The delivery this one failed over from or replays.</summary>
    public string? ParentDeliveryId { get; init; }

    /// <summary>Groups failures with the same cause (see <see cref="ClusterCount"/>).</summary>
    public string? Fingerprint { get; init; }

    /// <summary>The destination's <c>Retry-After</c> response header, if it sent one.</summary>
    public string? RetryAfter { get; init; }

    /// <summary>How many deliveries share <see cref="Fingerprint"/>; null without a fingerprint.</summary>
    public int? ClusterCount { get; init; }

    /// <summary>Known causes the failure matches; empty for a delivery that did not fail.</summary>
    public List<FailurePatternHint>? PatternHints { get; init; }

    /// <summary>The route's circuit breaker; null when the route no longer exists.</summary>
    public EventDebugRouteCircuit? RouteCircuit { get; init; }
}

/// <summary>The destination of an <see cref="EventDebugDelivery"/>.</summary>
public record EventDebugDestination
{
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
}

/// <summary>A known failure cause an <see cref="EventDebugDelivery"/> matches, with a suggested fix.</summary>
public record FailurePatternHint
{
    public string? Id { get; init; }

    /// <summary>e.g. <c>auth_signature</c>, <c>endpoint_missing</c>, <c>rate_limit</c>.</summary>
    public string? Category { get; init; }

    public string? Title { get; init; }
    public string? Hypothesis { get; init; }
    public string? SuggestedFix { get; init; }
}

/// <summary>The circuit breaker of an <see cref="EventDebugDelivery"/>'s route.</summary>
public record EventDebugRouteCircuit
{
    /// <summary><c>closed</c>, <c>open</c> or <c>half_open</c>.</summary>
    public string? State { get; init; }

    public string? OpenedAt { get; init; }
    public int? CooldownSeconds { get; init; }
    public int? FailureThreshold { get; init; }
    public int? ConsecutiveFailures { get; init; }
}

public record EventDebugRequest
{
    [JsonConverter(typeof(JsonStringStringDictionaryConverter))]
    public Dictionary<string, string>? Headers { get; init; }
    public long? PayloadSize { get; init; }
    public string? ContentType { get; init; }
}

public record EventDebugExtras
{
    public string? CurlCommand { get; init; }
}

public record EventDebugData
{
    public string? Id { get; init; }
    public string? SourceId { get; init; }
    public string? EventType { get; init; }
    [JsonConverter(typeof(JsonStringStringDictionaryConverter))]
    public Dictionary<string, string>? Headers { get; init; }
    public object? Payload { get; init; }
    public object? SignatureValid { get; init; }
    public string? ReceivedAt { get; init; }
    public string? IpAddress { get; init; }
    public string? SourceName { get; init; }
    public string? SourceSlug { get; init; }
    public string? Provider { get; init; }
    public string? PayloadHash { get; init; }

    /// <summary><c>unsigned</c> when the sender attached no signature at all; otherwise null.</summary>
    public string? SignatureIssue { get; init; }
}

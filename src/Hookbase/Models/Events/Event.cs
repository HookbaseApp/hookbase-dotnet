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

    /// <summary>Deliveries with transform output, circuit state and failure hints.</summary>
    public System.Text.Json.JsonElement? Deliveries { get; init; }

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

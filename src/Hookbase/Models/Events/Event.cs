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
    public EventDebugData? Event { get; init; }
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
}

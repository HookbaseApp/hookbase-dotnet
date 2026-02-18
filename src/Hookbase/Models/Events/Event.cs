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
    public DeliveryStats? DeliveryStats { get; init; }
}

/// <summary>
/// Detailed event information including payload and deliveries.
/// </summary>
public record EventDetail
{
    public string? Id { get; init; }
    public string? SourceId { get; init; }
    public string? EventType { get; init; }
    public object? Payload { get; init; }
    [JsonConverter(typeof(JsonStringStringDictionaryConverter))]
    public Dictionary<string, string>? Headers { get; init; }
    public object? SignatureValid { get; init; }
    public string? ReceivedAt { get; init; }
    public string? IpAddress { get; init; }
    public string? SourceName { get; init; }
    public List<EventDeliveryInfo>? Deliveries { get; init; }
}

public record EventDeliveryInfo
{
    public string? Id { get; init; }
    public string? DestinationId { get; init; }
    public string? DestinationName { get; init; }
    public string? DestinationUrl { get; init; }
    public string? Status { get; init; }
    public int? StatusCode { get; init; }
    public int Attempts { get; init; }
    public string? CreatedAt { get; init; }
    public string? CompletedAt { get; init; }
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

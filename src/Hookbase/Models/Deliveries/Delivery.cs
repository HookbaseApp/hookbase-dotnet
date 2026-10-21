using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Deliveries;

/// <summary>
/// Webhook delivery attempt: one event sent to one destination by one route.
/// </summary>
/// <remarks>
/// Fields match <c>GET /api/deliveries</c> and <c>GET /api/deliveries/{id}</c>.
/// <c>DestinationUrl</c> is set by <c>GetAsync</c> and on event details, not on list;
/// <c>EventType</c> is set on list only.
/// </remarks>
public record Delivery
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? EventId { get; init; }
    public string? RouteId { get; init; }
    public string? DestinationId { get; init; }

    /// <summary>
    /// e.g. <c>pending</c>, <c>delivered</c>, <c>failed</c>. <c>pending</c> covers both not-yet-sent
    /// and failed-and-waiting-to-retry (then <see cref="NextRetryAt"/> is set).
    /// </summary>
    public string? Status { get; init; }

    /// <summary>Attempts made so far; null for a delivery that has not been counted yet.</summary>
    public int? AttemptCount { get; init; }

    public int? MaxAttempts { get; init; }
    public string? NextRetryAt { get; init; }

    /// <summary>The destination's HTTP status, or null when no response came back.</summary>
    public int? ResponseStatus { get; init; }

    public string? ResponseKey { get; init; }

    [JsonConverter(typeof(JsonStringStringDictionaryConverter))]
    public Dictionary<string, string>? ResponseHeaders { get; init; }

    public string? ResponseBody { get; init; }

    /// <summary>Round-trip time of the last attempt, in milliseconds.</summary>
    public int? LatencyMs { get; init; }

    public string? ErrorMessage { get; init; }
    public string? DeliveredAt { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
    public string? DestinationName { get; init; }
    public string? DestinationUrl { get; init; }
    public string? EventType { get; init; }

    /// <summary>Same as <see cref="ResponseStatus"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is responseStatus. Use ResponseStatus.")]
    public int? StatusCode => ResponseStatus;

    /// <summary>Same as <see cref="AttemptCount"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is attemptCount. Use AttemptCount.")]
    public int Attempts => AttemptCount ?? 0;

    /// <summary>Same as <see cref="ErrorMessage"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is errorMessage. Use ErrorMessage.")]
    public string? Error => ErrorMessage;

    /// <summary>Same as <see cref="LatencyMs"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is latencyMs. Use LatencyMs.")]
    public int? Duration => LatencyMs;

    /// <summary>Same as <see cref="DeliveredAt"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is deliveredAt, which is only set for delivered attempts. Use DeliveredAt with Status.")]
    public string? CompletedAt => DeliveredAt;
}

/// <summary>
/// Detailed delivery information (<c>GET /api/deliveries/{id}</c>).
/// </summary>
public record DeliveryDetail : Delivery
{
    [JsonIgnore]
    [Obsolete("The API does not return this field; it is always null. Use EventId.")]
    public EventInfo? Event { get; init; }

    /// <summary>Built from <see cref="Delivery.DestinationName"/> and <see cref="Delivery.DestinationUrl"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API returns destinationName and destinationUrl at the top level. Use DestinationName and DestinationUrl.")]
    public DestinationInfo? Destination => DestinationName == null && DestinationUrl == null
        ? null
        : new DestinationInfo { Name = DestinationName, Url = DestinationUrl };
}

public record EventInfo
{
    public string? Id { get; init; }
    public string? EventType { get; init; }
    public string? ReceivedAt { get; init; }
}

public record DestinationInfo
{
    public string? Name { get; init; }
    public string? Url { get; init; }
}

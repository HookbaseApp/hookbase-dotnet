namespace Hookbase.Models.Deliveries;

/// <summary>
/// Webhook delivery attempt.
/// </summary>
public record Delivery
{
    public string? Id { get; init; }
    public string? EventId { get; init; }
    public string? RouteId { get; init; }
    public string? DestinationId { get; init; }
    public string? OrganizationId { get; init; }
    public string? Status { get; init; }
    public int? StatusCode { get; init; }
    public int Attempts { get; init; }
    public int MaxAttempts { get; init; }
    public string? ResponseBody { get; init; }
    public string? Error { get; init; }
    public int? Duration { get; init; }
    public string? CreatedAt { get; init; }
    public string? CompletedAt { get; init; }
    public string? NextRetryAt { get; init; }
}

/// <summary>
/// Detailed delivery information.
/// </summary>
public record DeliveryDetail : Delivery
{
    public EventInfo? Event { get; init; }
    public DestinationInfo? Destination { get; init; }
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

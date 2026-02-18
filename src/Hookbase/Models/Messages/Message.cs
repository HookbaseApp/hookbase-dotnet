namespace Hookbase.Models.Messages;

/// <summary>
/// Outbound webhook message.
/// </summary>
public record Message
{
    public string? Id { get; init; }
    public string? ApplicationId { get; init; }
    public string? EventType { get; init; }
    public string? EventId { get; init; }
    public Dictionary<string, object>? Payload { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    public string? CreatedAt { get; init; }
}

/// <summary>
/// Outbound message delivery attempt.
/// </summary>
public record OutboundMessage
{
    public string? Id { get; init; }
    public string? MessageId { get; init; }
    public string? EndpointId { get; init; }
    public string? EndpointUrl { get; init; }
    public string? EventType { get; init; }
    public string? Status { get; init; }
    public int Attempts { get; init; }
    public int MaxAttempts { get; init; }
    public string? LastAttemptAt { get; init; }
    public string? NextAttemptAt { get; init; }
    public int? LastResponseStatus { get; init; }
    public string? LastResponseBody { get; init; }
    public string? LastError { get; init; }
    public string? DeliveredAt { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Input for sending a new message.
/// </summary>
public record SendMessageRequest
{
    public required string EventType { get; init; }
    public required Dictionary<string, object> Payload { get; init; }
    public string? EventId { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    public List<string>? EndpointIds { get; init; }
}

/// <summary>
/// Response after sending a message.
/// </summary>
public record SendMessageResponse
{
    public string? MessageId { get; init; }
    public List<OutboundMessageInfo>? OutboundMessages { get; init; }
}

public record OutboundMessageInfo
{
    public string? Id { get; init; }
    public string? EndpointId { get; init; }
    public string? Status { get; init; }
}

/// <summary>
/// Message statistics summary.
/// </summary>
public record MessageStatsSummary
{
    public int TotalMessages { get; init; }
    public int PendingMessages { get; init; }
    public int DeliveredMessages { get; init; }
    public int FailedMessages { get; init; }
    public double SuccessRate { get; init; }
}

/// <summary>
/// Response after replaying a message.
/// </summary>
public record ReplayMessageResponse
{
    public string? OriginalMessageId { get; init; }
    public string? NewMessageId { get; init; }
    public string? Status { get; init; }
}

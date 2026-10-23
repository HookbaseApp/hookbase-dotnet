using System.Text.Json;
using System.Text.Json.Serialization;

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
/// An outbound message: one event's delivery to one endpoint.
/// </summary>
/// <remarks>
/// Field names follow <c>GET /outbound-messages</c>, <c>GET /outbound-messages/:id</c> and
/// <c>GET /outbound-messages/dlq/messages</c>. The list returns a subset; the detail-only and
/// DLQ-only members are null where the API does not send them.
/// </remarks>
public record OutboundMessage
{
    public string? Id { get; init; }

    /// <summary>The event this message delivers (the id <c>send-event</c> returned).</summary>
    public string? EventId { get; init; }

    public string? EventType { get; init; }
    public string? ApplicationId { get; init; }
    public string? ApplicationName { get; init; }
    public string? EndpointId { get; init; }
    public string? EndpointUrl { get; init; }

    /// <summary>
    /// pending, processing, success, failed, awaiting_retry, exhausted or dlq. Kept as a string so a
    /// status added later does not fail deserialization.
    /// </summary>
    public string? Status { get; init; }

    public int Attempts { get; init; }
    public int MaxAttempts { get; init; }
    public int? LastResponseStatus { get; init; }
    public string? LastErrorType { get; init; }
    public string? LastErrorMessage { get; init; }
    public string? CreatedAt { get; init; }

    /// <summary>When the message reached a final state (delivered, exhausted or discarded from the DLQ).</summary>
    public string? CompletedAt { get; init; }

    // Detail-only (GET /outbound-messages/:id)
    public string? SubscriptionId { get; init; }
    public string? EventTypeId { get; init; }
    public string? EndpointDescription { get; init; }
    public string? PayloadKey { get; init; }
    public long? PayloadSizeBytes { get; init; }
    public string? PayloadHash { get; init; }
    public JsonElement? Metadata { get; init; }
    public int? Priority { get; init; }

    /// <summary>The delivered payload, or null once retention has removed it.</summary>
    public JsonElement? Payload { get; init; }

    /// <summary>W3C trace id of the send this message belongs to; each attempt's span is in it.</summary>
    public string? TraceId { get; init; }

    /// <summary>The event-type version the payload was published in; null when unversioned.</summary>
    public string? EventVersion { get; init; }

    /// <summary>The version the latest attempt delivered, after conversion to the subscription's pin.</summary>
    public string? DeliveredEventVersion { get; init; }

    // DLQ-only (GET /outbound-messages/dlq/messages)
    public string? DlqReason { get; init; }
    public string? DlqMovedAt { get; init; }
    public int? DlqRetryCount { get; init; }
    public string? DlqLastRetryAt { get; init; }

    /// <summary>Same as <see cref="EventId"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API has no messageId field; this returns EventId. Use EventId.")]
    public string? MessageId => EventId;

    /// <summary>Same as <see cref="LastErrorMessage"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is lastErrorMessage. Use LastErrorMessage.")]
    public string? LastError => LastErrorMessage;

    /// <summary>Same as <see cref="CompletedAt"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is completedAt, which is also set for messages that were not delivered. Use CompletedAt with Status.")]
    public string? DeliveredAt => CompletedAt;

    [JsonIgnore]
    [Obsolete("The API does not return this field; it is always null.")]
    public string? LastAttemptAt { get; init; }

    [JsonIgnore]
    [Obsolete("The API does not return this field; it is always null.")]
    public string? NextAttemptAt { get; init; }

    [JsonIgnore]
    [Obsolete("The API does not return this field; it is always null. Response bodies are on the attempts.")]
    public string? LastResponseBody { get; init; }

    [JsonIgnore]
    [Obsolete("The API does not return this field; it is always null.")]
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Input for sending a new event (<c>POST /send-event</c>).
/// </summary>
public record SendMessageRequest
{
    /// <summary>The event type name, for example <c>order.created</c>.</summary>
    public required string EventType { get; init; }

    public required Dictionary<string, object> Payload { get; init; }

    /// <summary>Send only to this application's subscribed endpoints.</summary>
    public string? ApplicationId { get; init; }

    /// <summary>Send only to this endpoint. Merged with <see cref="EndpointIds"/>.</summary>
    public string? EndpointId { get; init; }

    /// <summary>Send only to these endpoints.</summary>
    public List<string>? EndpointIds { get; init; }

    /// <summary>
    /// Makes the send safe to repeat: a second send with the same key does not deliver again. When
    /// unset the SDK generates one per call, so its own retries cannot duplicate the event.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Labels matched against subscription label filters.</summary>
    public Dictionary<string, string>? Labels { get; init; }

    /// <summary>Flat key-value metadata (string, number, boolean or null values).</summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>Priority override: 0=Critical, 1=High, 2=Normal, 3=Low, 4=Bulk.</summary>
    public int? Priority { get; init; }

    /// <summary>
    /// The event-type version the payload is in: <c>latest</c> (the default when unset) or a
    /// version label. Endpoints pinned to another version get it converted where the event type
    /// defines a conversion.
    /// </summary>
    public string? EventVersion { get; init; }

    /// <summary>
    /// The API has no eventId input and silently dropped it. A value set here is now used as the
    /// idempotency key when <see cref="IdempotencyKey"/> is not set, which is what it was documented to do.
    /// </summary>
    [Obsolete("The API ignores eventId. Use IdempotencyKey; a value set here is sent as the idempotency key.")]
    public string? EventId { get; init; }
}

/// <summary>
/// Response after sending an event.
/// </summary>
public record SendMessageResponse
{
    /// <summary>The id of the event that was created.</summary>
    public string? EventId { get; init; }

    /// <summary>How many endpoint deliveries were queued.</summary>
    public int MessagesQueued { get; init; }

    /// <summary>The endpoints the event was queued for.</summary>
    public List<SendMessageEndpoint>? Endpoints { get; init; }

    /// <summary>The event-type version the event was published in; null when unversioned.</summary>
    public string? EventVersion { get; init; }

    /// <summary>Same as <see cref="EventId"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API has no messageId field; this returns EventId. Use EventId.")]
    public string? MessageId => EventId;

    [JsonIgnore]
    [Obsolete("The API does not return this field; it is always null. Use Endpoints and MessagesQueued.")]
    public List<OutboundMessageInfo>? OutboundMessages { get; init; }
}

/// <summary>
/// An endpoint an event was queued for.
/// </summary>
public record SendMessageEndpoint
{
    public string? Id { get; init; }
    public string? Url { get; init; }
}

public record OutboundMessageInfo
{
    public string? Id { get; init; }
    public string? EndpointId { get; init; }
    public string? Status { get; init; }
}

/// <summary>
/// Outbound message counts by status (<c>GET /outbound-messages/stats/summary</c>).
/// </summary>
public record MessageStatsSummary
{
    public int Pending { get; init; }
    public int Processing { get; init; }
    public int Success { get; init; }
    public int Failed { get; init; }
    public int AwaitingRetry { get; init; }
    public int Exhausted { get; init; }
    public int Dlq { get; init; }
    public int Total { get; init; }

    /// <summary>Same as <see cref="Total"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is total. Use Total.")]
    public int TotalMessages => Total;

    /// <summary>Same as <see cref="Pending"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is pending. Use Pending.")]
    public int PendingMessages => Pending;

    /// <summary>Same as <see cref="Success"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is success. Use Success.")]
    public int DeliveredMessages => Success;

    /// <summary>Same as <see cref="Failed"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is failed. Use Failed, Exhausted and Dlq.")]
    public int FailedMessages => Failed;

    /// <summary><see cref="Success"/> divided by <see cref="Total"/>, or 0 when there are no messages.</summary>
    [JsonIgnore]
    [Obsolete("The API does not return a success rate; this is computed as Success / Total.")]
    public double SuccessRate => Total == 0 ? 0 : (double)Success / Total;
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

/// <summary>
/// Filters for <see cref="Resources.MessagesResource.ExportAsync(MessageExportOptions?, CancellationToken)"/>.
/// </summary>
public record MessageExportOptions
{
    /// <summary><c>json</c> (default) or <c>csv</c>.</summary>
    public string Format { get; init; } = "json";

    /// <summary><c>events</c> (default) or <c>messages</c>.</summary>
    public string Type { get; init; } = "events";

    /// <summary>ISO 8601 start of the range.</summary>
    public string? StartDate { get; init; }

    /// <summary>ISO 8601 end of the range.</summary>
    public string? EndDate { get; init; }

    public string? Status { get; init; }
    public string? EventType { get; init; }
    public string? ApplicationId { get; init; }

    /// <summary>Maximum rows; default 10000.</summary>
    public int Limit { get; init; } = 10000;
}

/// <summary>
/// One delivery attempt of an outbound message (<c>GET /outbound-messages/:id/attempts</c>).
/// </summary>
public record MessageAttempt
{
    public string? Id { get; init; }
    public string? MessageId { get; init; }
    public int AttemptNumber { get; init; }

    /// <summary>success, failed, timeout and so on; a string so a new status does not fail parsing.</summary>
    public string? Status { get; init; }

    public int? ResponseStatus { get; init; }

    /// <summary>Response headers as the API stored them (a JSON object or JSON text).</summary>
    public JsonElement? ResponseHeaders { get; init; }

    public string? ResponseBody { get; init; }
    public int? ResponseTimeMs { get; init; }
    public int? TotalTimeMs { get; init; }
    public string? ErrorType { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public string? RequestUrl { get; init; }

    /// <summary>Request headers as the API stored them (a JSON object or JSON text).</summary>
    public JsonElement? RequestHeaders { get; init; }

    public int? RequestBodySize { get; init; }

    /// <summary>What started the attempt, for example <c>system</c> or <c>manual</c>.</summary>
    public string? TriggeredBy { get; init; }

    public string? TriggeredByUser { get; init; }
    public string? CreatedAt { get; init; }
    public string? CompletedAt { get; init; }
    public string? EventVersion { get; init; }
}

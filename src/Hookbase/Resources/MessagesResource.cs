using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Messages;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing outbound webhook messages.
/// </summary>
public class MessagesResource : BaseResource
{
    public MessagesResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// Send a new outbound webhook event.
    /// </summary>
    /// <remarks>
    /// The request always carries an idempotency key, in the body and as the <c>Idempotency-Key</c>
    /// header: <see cref="SendMessageRequest.IdempotencyKey"/> when set, otherwise one generated for
    /// this call. A retry after a timeout or 5xx therefore cannot deliver the event twice.
    /// </remarks>
    public async Task<SendMessageResponse> SendAsync(
        string organizationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable CS0618 // EventId is obsolete; honour it as the key it was documented to be.
        var idempotencyKey = request.IdempotencyKey ?? request.EventId ?? GenerateIdempotencyKey();
#pragma warning restore CS0618

        var response = await ApiClient.RequestAsync<SendMessageDataResponse>(
            HttpMethod.Post,
            $"/api/organizations/{Uri.EscapeDataString(organizationId)}/send-event",
            body: BuildSendEventBody(request, idempotencyKey),
            idempotencyKey: idempotencyKey,
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// The send-event body. Built by hand rather than serialized from the request so that the
    /// endpoint targeting and idempotency key are always sent in the form the API reads.
    /// </summary>
    private static Dictionary<string, object> BuildSendEventBody(SendMessageRequest request, string idempotencyKey)
    {
        var body = new Dictionary<string, object>
        {
            ["eventType"] = request.EventType,
            ["payload"] = request.Payload,
            ["idempotencyKey"] = idempotencyKey
        };

        if (request.ApplicationId != null) body["applicationId"] = request.ApplicationId;

        var endpointIds = new List<string>();
        if (request.EndpointIds != null) endpointIds.AddRange(request.EndpointIds);
        if (request.EndpointId != null) endpointIds.Add(request.EndpointId);
        endpointIds = endpointIds.Distinct(StringComparer.Ordinal).ToList();

        if (endpointIds.Count > 0)
        {
            body["endpointIds"] = endpointIds;
            // Older API deployments read only the singular field and ignore the plural one.
            if (endpointIds.Count == 1) body["endpointId"] = endpointIds[0];
        }

        if (request.Labels != null) body["labels"] = request.Labels;
        if (request.Metadata != null) body["metadata"] = request.Metadata;
        if (request.Priority != null) body["priority"] = request.Priority.Value;

        return body;
    }

    /// <summary>
    /// List outbound messages with filtering.
    /// </summary>
    public async Task<CursorPage<OutboundMessage>> ListAsync(
        string organizationId,
        int limit = 50,
        string? cursor = null,
        string? status = null,
        string? eventType = null,
        string? applicationId = null,
        string? endpointId = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string> { ["limit"] = limit.ToString() };
        if (cursor != null) queryParams["cursor"] = cursor;
        if (status != null) queryParams["status"] = status;
        if (eventType != null) queryParams["eventType"] = eventType;
        if (applicationId != null) queryParams["applicationId"] = applicationId;
        if (endpointId != null) queryParams["endpointId"] = endpointId;

        var response = await ApiClient.RequestAsync<ListMessagesResponse>(
            HttpMethod.Get,
            $"/api/organizations/{Uri.EscapeDataString(organizationId)}/outbound-messages",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new CursorPage<OutboundMessage>
        {
            Data = response.Data,
            HasMore = response.Pagination.HasMore,
            NextCursor = response.Pagination.NextCursor
        };
    }

    /// <summary>
    /// Get a single outbound message by ID.
    /// </summary>
    public async Task<OutboundMessage> GetAsync(
        string organizationId,
        string id,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetMessageResponse>(
            HttpMethod.Get,
            $"/api/organizations/{Uri.EscapeDataString(organizationId)}/outbound-messages/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Replay a failed or exhausted message.
    /// </summary>
    public async Task<ReplayMessageResponse> ReplayAsync(
        string organizationId,
        string id,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<ReplayMessageDataResponse>(
            HttpMethod.Post,
            $"/api/organizations/{Uri.EscapeDataString(organizationId)}/outbound-messages/{Uri.EscapeDataString(id)}/replay",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Get delivery statistics summary.
    /// </summary>
    public async Task<MessageStatsSummary> GetStatsSummaryAsync(
        string organizationId,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetStatsSummaryResponse>(
            HttpMethod.Get,
            $"/api/organizations/{Uri.EscapeDataString(organizationId)}/outbound-messages/stats/summary",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Export outbound events or messages. Returns the response body as is: a CSV document, or a
    /// JSON document (<c>{ exportedAt, organization, count, events | messages }</c>).
    /// </summary>
    public async Task<string> ExportAsync(
        string organizationId,
        string format = "json",
        string type = "events",
        string? startDate = null,
        string? endDate = null,
        string? status = null,
        string? eventType = null,
        string? applicationId = null,
        int limit = 10000,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["format"] = format,
            ["type"] = type,
            ["limit"] = limit.ToString()
        };

        if (startDate != null) queryParams["startDate"] = startDate;
        if (endDate != null) queryParams["endDate"] = endDate;
        if (status != null) queryParams["status"] = status;
        if (eventType != null) queryParams["eventType"] = eventType;
        if (applicationId != null) queryParams["applicationId"] = applicationId;

        // A string response type returns the raw body rather than parsing it as JSON.
        return await ApiClient.RequestAsync<string>(
            HttpMethod.Get,
            $"/api/organizations/{Uri.EscapeDataString(organizationId)}/outbound-messages/export",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );
    }

    // Internal response types
    private record ListMessagesResponse
    {
        public required List<OutboundMessage> Data { get; init; }
        public required CursorPaginationInfo Pagination { get; init; }
    }

    private record GetMessageResponse
    {
        public required OutboundMessage Data { get; init; }
    }

    private record SendMessageDataResponse
    {
        public required SendMessageResponse Data { get; init; }
    }

    private record ReplayMessageDataResponse
    {
        public required ReplayMessageResponse Data { get; init; }
    }

    private record GetStatsSummaryResponse
    {
        public required MessageStatsSummary Data { get; init; }
    }
}

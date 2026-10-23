using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Messages;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing outbound webhook messages.
/// </summary>
/// <remarks>
/// Every method has an overload without an organization ID: the API resolves the organization
/// from the API key, as the other Hookbase SDKs do. The overloads that take an organization ID
/// call the organization-scoped routes, which answer 403 when the ID is not the key's own.
/// </remarks>
public class MessagesResource : BaseResource
{
    public MessagesResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>The route prefix: org-scoped when an organization ID is given, else the key's org.</summary>
    private static string Prefix(string? organizationId) =>
        organizationId == null ? "/api" : $"/api/organizations/{Uri.EscapeDataString(organizationId)}";

    /// <summary>
    /// Send a new outbound webhook event to the API key's organization.
    /// </summary>
    /// <remarks>
    /// The request always carries an idempotency key, in the body and as the <c>Idempotency-Key</c>
    /// header: <see cref="SendMessageRequest.IdempotencyKey"/> when set, otherwise one generated for
    /// this call. A retry after a timeout or 5xx therefore cannot deliver the event twice.
    /// </remarks>
    public Task<SendMessageResponse> SendAsync(
        SendMessageRequest request,
        CancellationToken cancellationToken = default) =>
        SendCoreAsync(null, request, cancellationToken);

    /// <summary>List the API key's organization's outbound messages.</summary>
    public Task<CursorPage<OutboundMessage>> ListAsync(
        int limit = 50,
        string? cursor = null,
        string? status = null,
        string? eventType = null,
        string? applicationId = null,
        string? endpointId = null,
        CancellationToken cancellationToken = default) =>
        ListCoreAsync(null, limit, cursor, status, eventType, applicationId, endpointId, cancellationToken);

    /// <summary>Get a single outbound message by ID.</summary>
    public Task<OutboundMessage> GetAsync(string id, CancellationToken cancellationToken = default) =>
        GetCoreAsync(null, id, cancellationToken);

    /// <summary>Replay a failed or exhausted message.</summary>
    public Task<ReplayMessageResponse> ReplayAsync(string id, CancellationToken cancellationToken = default) =>
        ReplayCoreAsync(null, id, cancellationToken);

    /// <summary>Get delivery statistics summary.</summary>
    public Task<MessageStatsSummary> GetStatsSummaryAsync(CancellationToken cancellationToken = default) =>
        GetStatsSummaryCoreAsync(null, cancellationToken);

    /// <summary>
    /// Export outbound events or messages of the API key's organization. Returns the response body
    /// as is: a CSV document, or a JSON document (<c>{ exportedAt, organization, count, events | messages }</c>).
    /// </summary>
    public Task<string> ExportAsync(MessageExportOptions? options, CancellationToken cancellationToken = default)
    {
        options ??= new MessageExportOptions();
        return ExportCoreAsync(null, options.Format, options.Type, options.StartDate, options.EndDate,
            options.Status, options.EventType, options.ApplicationId, options.Limit, cancellationToken);
    }

    /// <summary>
    /// List every delivery attempt of a message, newest first (<c>GET /outbound-messages/:id/attempts</c>).
    /// </summary>
    public async Task<List<MessageAttempt>> ListAttemptsAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<ListAttemptsResponse>(
            HttpMethod.Get,
            $"/api/outbound-messages/{Uri.EscapeDataString(id)}/attempts",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Send a new outbound webhook event through the organization-scoped route.
    /// <paramref name="organizationId"/> must be the API key's organization; the overload without
    /// it does the same.
    /// </summary>
    /// <remarks>
    /// The request always carries an idempotency key, in the body and as the <c>Idempotency-Key</c>
    /// header: <see cref="SendMessageRequest.IdempotencyKey"/> when set, otherwise one generated for
    /// this call. A retry after a timeout or 5xx therefore cannot deliver the event twice.
    /// </remarks>
    public Task<SendMessageResponse> SendAsync(
        string organizationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default) =>
        SendCoreAsync(organizationId, request, cancellationToken);

    private async Task<SendMessageResponse> SendCoreAsync(
        string? organizationId,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
#pragma warning disable CS0618 // EventId is obsolete; honour it as the key it was documented to be.
        var idempotencyKey = request.IdempotencyKey ?? request.EventId ?? GenerateIdempotencyKey();
#pragma warning restore CS0618

        var response = await ApiClient.RequestAsync<SendMessageDataResponse>(
            HttpMethod.Post,
            Prefix(organizationId) + "/send-event",
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
        if (request.EventVersion != null) body["eventVersion"] = request.EventVersion;

        return body;
    }

    /// <summary>
    /// List outbound messages with filtering.
    /// </summary>
    public Task<CursorPage<OutboundMessage>> ListAsync(
        string organizationId,
        int limit = 50,
        string? cursor = null,
        string? status = null,
        string? eventType = null,
        string? applicationId = null,
        string? endpointId = null,
        CancellationToken cancellationToken = default) =>
        ListCoreAsync(organizationId, limit, cursor, status, eventType, applicationId, endpointId, cancellationToken);

    private async Task<CursorPage<OutboundMessage>> ListCoreAsync(
        string? organizationId,
        int limit,
        string? cursor,
        string? status,
        string? eventType,
        string? applicationId,
        string? endpointId,
        CancellationToken cancellationToken)
    {
        var queryParams = new Dictionary<string, string> { ["limit"] = limit.ToString() };
        if (cursor != null) queryParams["cursor"] = cursor;
        if (status != null) queryParams["status"] = status;
        if (eventType != null) queryParams["eventType"] = eventType;
        if (applicationId != null) queryParams["applicationId"] = applicationId;
        if (endpointId != null) queryParams["endpointId"] = endpointId;

        var response = await ApiClient.RequestAsync<ListMessagesResponse>(
            HttpMethod.Get,
            Prefix(organizationId) + "/outbound-messages",
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
    public Task<OutboundMessage> GetAsync(
        string organizationId,
        string id,
        CancellationToken cancellationToken = default) =>
        GetCoreAsync(organizationId, id, cancellationToken);

    private async Task<OutboundMessage> GetCoreAsync(string? organizationId, string id, CancellationToken cancellationToken)
    {
        var response = await ApiClient.RequestAsync<GetMessageResponse>(
            HttpMethod.Get,
            Prefix(organizationId) + $"/outbound-messages/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Replay a failed or exhausted message.
    /// </summary>
    public Task<ReplayMessageResponse> ReplayAsync(
        string organizationId,
        string id,
        CancellationToken cancellationToken = default) =>
        ReplayCoreAsync(organizationId, id, cancellationToken);

    private async Task<ReplayMessageResponse> ReplayCoreAsync(string? organizationId, string id, CancellationToken cancellationToken)
    {
        var response = await ApiClient.RequestAsync<ReplayMessageDataResponse>(
            HttpMethod.Post,
            Prefix(organizationId) + $"/outbound-messages/{Uri.EscapeDataString(id)}/replay",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Get delivery statistics summary.
    /// </summary>
    public Task<MessageStatsSummary> GetStatsSummaryAsync(
        string organizationId,
        CancellationToken cancellationToken = default) =>
        GetStatsSummaryCoreAsync(organizationId, cancellationToken);

    private async Task<MessageStatsSummary> GetStatsSummaryCoreAsync(string? organizationId, CancellationToken cancellationToken)
    {
        var response = await ApiClient.RequestAsync<GetStatsSummaryResponse>(
            HttpMethod.Get,
            Prefix(organizationId) + "/outbound-messages/stats/summary",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Export outbound events or messages. Returns the response body as is: a CSV document, or a
    /// JSON document (<c>{ exportedAt, organization, count, events | messages }</c>).
    /// </summary>
    public Task<string> ExportAsync(
        string organizationId,
        string format = "json",
        string type = "events",
        string? startDate = null,
        string? endDate = null,
        string? status = null,
        string? eventType = null,
        string? applicationId = null,
        int limit = 10000,
        CancellationToken cancellationToken = default) =>
        ExportCoreAsync(organizationId, format, type, startDate, endDate, status, eventType, applicationId, limit, cancellationToken);

    private async Task<string> ExportCoreAsync(
        string? organizationId,
        string format,
        string type,
        string? startDate,
        string? endDate,
        string? status,
        string? eventType,
        string? applicationId,
        int limit,
        CancellationToken cancellationToken)
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
            Prefix(organizationId) + "/outbound-messages/export",
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

    private record ListAttemptsResponse
    {
        public required List<MessageAttempt> Data { get; init; }
    }

    private record GetStatsSummaryResponse
    {
        public required MessageStatsSummary Data { get; init; }
    }
}

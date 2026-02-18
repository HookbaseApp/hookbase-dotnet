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
    public async Task<SendMessageResponse> SendAsync(
        string organizationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<SendMessageDataResponse>(
            HttpMethod.Post,
            $"/api/organizations/{Uri.EscapeDataString(organizationId)}/send-event",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Data;
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
    /// Export outbound messages (events or messages) as JSON or CSV.
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

        // Export returns raw CSV or JSON string
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

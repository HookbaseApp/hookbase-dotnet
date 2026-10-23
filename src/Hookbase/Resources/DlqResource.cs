using System.Text.Json.Serialization;
using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Messages;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing dead letter queue messages.
/// </summary>
public class DlqResource : BaseResource
{
    public DlqResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List messages in the dead letter queue.
    /// </summary>
    public async Task<CursorPage<OutboundMessage>> ListAsync(
        int limit = 50,
        string? cursor = null,
        string? applicationId = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["limit"] = limit.ToString()
        };

        if (cursor != null) queryParams["cursor"] = cursor;
        if (applicationId != null) queryParams["applicationId"] = applicationId;

        var response = await ApiClient.RequestAsync<ListDlqResponse>(
            HttpMethod.Get,
            "/api/outbound-messages/dlq/messages",
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
    /// List messages in the dead letter queue with every filter the API supports.
    /// </summary>
    public async Task<CursorPage<OutboundMessage>> ListAsync(
        DlqListOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var queryParams = new Dictionary<string, string> { ["limit"] = options.Limit.ToString() };
        if (options.Cursor != null) queryParams["cursor"] = options.Cursor;
        if (options.ApplicationId != null) queryParams["applicationId"] = options.ApplicationId;
        if (options.EndpointId != null) queryParams["endpointId"] = options.EndpointId;
        if (options.EventType != null) queryParams["eventType"] = options.EventType;
        if (options.Reason != null) queryParams["reason"] = options.Reason;
        if (options.StartDate != null) queryParams["startDate"] = options.StartDate;
        if (options.EndDate != null) queryParams["endDate"] = options.EndDate;

        var response = await ApiClient.RequestAsync<ListDlqResponse>(
            HttpMethod.Get,
            "/api/outbound-messages/dlq/messages",
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
    /// Get DLQ statistics.
    /// </summary>
    public async Task<DlqStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<DlqStatsResponse>(
            HttpMethod.Get,
            "/api/outbound-messages/dlq/stats",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Retry a message from the DLQ. The retry is a new message; its id is
    /// <see cref="ReplayMessageResponse.NewMessageId"/>.
    /// </summary>
    public async Task<ReplayMessageResponse> RetryAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<RetryResponse>(
            HttpMethod.Post,
            $"/api/outbound-messages/dlq/{Uri.EscapeDataString(id)}/retry",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Delete a message from the DLQ.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Delete,
            $"/api/outbound-messages/dlq/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Retry up to 100 DLQ messages at once (<c>POST /outbound-messages/dlq/retry-bulk</c>). Ids that
    /// are not in the DLQ are skipped; a 404 means none of them were.
    /// </summary>
    public async Task<DlqBulkRetryResult> RetryBulkAsync(
        IEnumerable<string> messageIds,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<DataResponse<DlqBulkRetryResult>>(
            HttpMethod.Post,
            "/api/outbound-messages/dlq/retry-bulk",
            body: new { messageIds = messageIds.ToList() },
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Delete up to 100 DLQ messages at once (<c>DELETE /outbound-messages/dlq/bulk</c>).
    /// </summary>
    /// <returns>The number of ids sent; the API does not report how many were in the DLQ.</returns>
    public async Task<int> DeleteBulkAsync(
        IEnumerable<string> messageIds,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<DataResponse<DlqBulkDeleteResult>>(
            HttpMethod.Delete,
            "/api/outbound-messages/dlq/bulk",
            body: new { messageIds = messageIds.ToList() },
            cancellationToken: cancellationToken
        );

        return response.Data.Deleted;
    }

    // Internal response types
    private record DataResponse<T>
    {
        public required T Data { get; init; }
    }

    private record DlqBulkDeleteResult
    {
        public int Deleted { get; init; }
    }

    private record ListDlqResponse
    {
        public required List<OutboundMessage> Data { get; init; }
        public required CursorPaginationInfo Pagination { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }

    private record DlqStatsResponse
    {
        public required DlqStats Data { get; init; }
    }

    private record RetryResponse
    {
        public required ReplayMessageResponse Data { get; init; }
    }
}

/// <summary>
/// DLQ statistics (<c>GET /outbound-messages/dlq/stats</c>).
/// </summary>
public record DlqStats
{
    /// <summary>Messages currently in the DLQ.</summary>
    public int Total { get; init; }

    /// <summary>Counts keyed by DLQ reason (<c>unknown</c> when none was recorded).</summary>
    public Dictionary<string, int>? ByReason { get; init; }

    /// <summary>The ten endpoints with the most DLQ messages.</summary>
    public List<DlqEndpointCount>? ByEndpoint { get; init; }

    /// <summary>The ten event types with the most DLQ messages.</summary>
    public List<DlqEventTypeCount>? ByEventType { get; init; }

    /// <summary>Same as <see cref="Total"/>.</summary>
    [JsonIgnore]
    [Obsolete("The API field is total. Use Total.")]
    public int TotalMessages => Total;

    [JsonIgnore]
    [Obsolete("The API does not return per-application counts; this is always null. Use ByEndpoint or ByEventType.")]
    public Dictionary<string, int>? MessagesByApplication { get; init; }
}

/// <summary>DLQ message count for one endpoint.</summary>
public record DlqEndpointCount
{
    public string? EndpointId { get; init; }
    public string? EndpointUrl { get; init; }
    public int Count { get; init; }
}

/// <summary>DLQ message count for one event type.</summary>
public record DlqEventTypeCount
{
    public string? EventType { get; init; }
    public int Count { get; init; }
}

/// <summary>Filters for <see cref="DlqResource.ListAsync(DlqListOptions, CancellationToken)"/>.</summary>
public record DlqListOptions
{
    /// <summary>Page size, at most 100. Default 50.</summary>
    public int Limit { get; init; } = 50;
    public string? Cursor { get; init; }
    public string? ApplicationId { get; init; }
    public string? EndpointId { get; init; }
    public string? EventType { get; init; }

    /// <summary>DLQ reason, for example <c>max_retries_exceeded</c> (sent as <c>reason</c>).</summary>
    public string? Reason { get; init; }

    /// <summary>ISO 8601; messages moved to the DLQ at or after this time.</summary>
    public string? StartDate { get; init; }

    /// <summary>ISO 8601; messages moved to the DLQ at or before this time.</summary>
    public string? EndDate { get; init; }
}

/// <summary>Result of <see cref="DlqResource.RetryBulkAsync"/>.</summary>
public record DlqBulkRetryResult
{
    /// <summary>How many retries were queued.</summary>
    public int Queued { get; init; }

    /// <summary>How many of the ids were not in the DLQ.</summary>
    public int Skipped { get; init; }

    /// <summary>One entry per queued retry: the original id and the new message's id.</summary>
    public List<ReplayMessageResponse>? Messages { get; init; }
}

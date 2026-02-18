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
    /// Get DLQ statistics.
    /// </summary>
    public async Task<DlqStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<DlqStats>(
            HttpMethod.Get,
            "/api/outbound-messages/dlq/stats",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Retry a message from the DLQ.
    /// </summary>
    public async Task RetryAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Post,
            $"/api/outbound-messages/dlq/{Uri.EscapeDataString(id)}/retry",
            cancellationToken: cancellationToken
        );
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

    // Internal response types
    private record ListDlqResponse
    {
        public required List<OutboundMessage> Data { get; init; }
        public required CursorPaginationInfo Pagination { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

/// <summary>
/// DLQ statistics.
/// </summary>
public record DlqStats
{
    public int TotalMessages { get; init; }
    public Dictionary<string, int>? MessagesByApplication { get; init; }
}

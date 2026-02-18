using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Events;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for viewing inbound webhook events.
/// </summary>
public class EventsResource : BaseResource
{
    public EventsResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List all events with offset-based pagination.
    /// API uses limit/offset (not page/pageSize).
    /// </summary>
    public async Task<OffsetPage<InboundEvent>> ListAsync(
        int limit = 20,
        int offset = 0,
        string? sourceId = null,
        InboundEventStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["limit"] = limit.ToString(),
            ["offset"] = offset.ToString()
        };

        if (sourceId != null) queryParams["sourceId"] = sourceId;
        if (status.HasValue) queryParams["status"] = status.Value.ToString().ToLower();

        var response = await ApiClient.RequestAsync<ListEventsResponse>(
            HttpMethod.Get,
            "/api/events",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new OffsetPage<InboundEvent>
        {
            Data = response.Events,
            Total = response.Total,
            Page = offset / limit + 1,
            PageSize = limit
        };
    }

    /// <summary>
    /// Get detailed information about an event.
    /// </summary>
    public async Task<EventDetail> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetEventResponse>(
            HttpMethod.Get,
            $"/api/events/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Event;
    }

    /// <summary>
    /// Get debug information for an event.
    /// </summary>
    public async Task<EventDebugInfo> DebugAsync(string id, CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<EventDebugInfo>(
            HttpMethod.Get,
            $"/api/events/{Uri.EscapeDataString(id)}/debug",
            cancellationToken: cancellationToken
        );
    }

    // Internal response types
    private record ListEventsResponse
    {
        public required List<InboundEvent> Events { get; init; }
        public int Total { get; init; }
        public int Limit { get; init; }
        public int Offset { get; init; }
    }

    private record GetEventResponse
    {
        public required EventDetail Event { get; init; }
    }
}

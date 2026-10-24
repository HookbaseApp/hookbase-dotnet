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
    /// One page of events, newest first. Pass the returned <see cref="CursorPage{T}.NextCursor"/>
    /// back as <paramref name="cursor"/> for the next page.
    /// </summary>
    /// <remarks>
    /// The API is keyset-paginated and caps <paramref name="limit"/> at 100.
    /// <paramref name="sourceId"/> takes a source ID or slug. An unknown one throws
    /// <see cref="Hookbase.Exceptions.NotFoundException"/> rather than returning an empty page.
    /// </remarks>
    public async Task<CursorPage<InboundEvent>> ListPageAsync(
        int limit = 50,
        string? cursor = null,
        string? sourceId = null,
        InboundEventStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["limit"] = limit.ToString()
        };

        if (cursor != null) queryParams["cursor"] = cursor;
        if (sourceId != null) queryParams["sourceId"] = sourceId;
        if (status.HasValue) queryParams["status"] = Hookbase.Json.WireNames.SnakeCase(status.Value);

        var response = await ApiClient.RequestAsync<ListEventsResponse>(
            HttpMethod.Get,
            "/api/events",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new CursorPage<InboundEvent>
        {
            Data = response.Events,
            HasMore = response.HasMore && response.NextCursor != null,
            NextCursor = response.NextCursor
        };
    }

    /// <summary>
    /// Iterate through all matching events, newest first, following the API's cursor.
    /// </summary>
    public async IAsyncEnumerable<InboundEvent> ListAllAsync(
        string? sourceId = null,
        InboundEventStatus? status = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? cursor = null;

        while (true)
        {
            var page = await ListPageAsync(100, cursor, sourceId, status, cancellationToken);
            foreach (var item in page.Data)
            {
                yield return item;
            }
            if (!page.HasMore || page.Data.Count == 0) yield break;
            cursor = page.NextCursor;
        }
    }

    /// <summary>
    /// List events with offset-style paging.
    /// </summary>
    /// <remarks>
    /// The API is cursor-paginated and ignores <c>offset</c>, so a non-zero
    /// <paramref name="offset"/> is reached by walking the cursor from the first event (one
    /// request per 100 events skipped). <see cref="OffsetPage{T}.Total"/> is not a real total: the
    /// API does not count events by default, so it is set just high enough for
    /// <see cref="OffsetPage{T}.HasMore"/> to be correct.
    /// <paramref name="sourceId"/> takes a source ID or slug. An unknown one throws
    /// <see cref="Hookbase.Exceptions.NotFoundException"/> rather than returning an empty page.
    /// </remarks>
    [Obsolete("The events API is cursor-paginated. Use ListPageAsync with a cursor, or ListAllAsync.")]
    public async Task<OffsetPage<InboundEvent>> ListAsync(
        int limit = 20,
        int offset = 0,
        string? sourceId = null,
        InboundEventStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(offset, 0);

        string? cursor = null;
        var skip = offset;
        while (skip > 0)
        {
            var skipped = await ListPageAsync(Math.Min(skip, 100), cursor, sourceId, status, cancellationToken);
            skip -= skipped.Data.Count;
            if (!skipped.HasMore || skipped.Data.Count == 0)
            {
                return new OffsetPage<InboundEvent>
                {
                    Data = new List<InboundEvent>(),
                    Total = offset - skip,
                    Page = offset / limit + 1,
                    PageSize = limit
                };
            }
            cursor = skipped.NextCursor;
        }

        var page = await ListPageAsync(limit, cursor, sourceId, status, cancellationToken);

        return new OffsetPage<InboundEvent>
        {
            Data = page.Data,
            // Page * PageSize == offset + limit for aligned offsets, so one past it means "more".
            Total = offset + page.Data.Count + (page.HasMore ? 1 : 0),
            Page = offset / limit + 1,
            PageSize = limit
        };
    }

    /// <summary>
    /// Get detailed information about an event, with its payload and deliveries.
    /// </summary>
    public async Task<EventDetail> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetEventResponse>(
            HttpMethod.Get,
            $"/api/events/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        // The API returns { event, payload, transient, deliveries }; fold them into one object.
        return response.Event with
        {
            Payload = response.Payload,
            Transient = response.Transient,
            Deliveries = response.Deliveries ?? new List<EventDeliveryInfo>()
        };
    }

    /// <summary>
    /// Get debug information for an event.
    /// </summary>
    public async Task<EventDebugInfo> DebugAsync(string id, CancellationToken cancellationToken = default)
    {
        var info = await ApiClient.RequestAsync<EventDebugInfo>(
            HttpMethod.Get,
            $"/api/events/{Uri.EscapeDataString(id)}/debug",
            cancellationToken: cancellationToken
        );

        // The API puts the headers under request and the payload at the top level, not on event.
        if (info.Event != null)
        {
            info = info with
            {
                Event = info.Event with
                {
                    Headers = info.Event.Headers ?? info.Request?.Headers,
                    Payload = info.Event.Payload ?? info.Payload,
                }
            };
        }

        return info;
    }

    // Internal response types
    private record ListEventsResponse
    {
        public required List<InboundEvent> Events { get; init; }
        public int? Total { get; init; }
        public int Limit { get; init; }
        public bool HasMore { get; init; }
        public string? NextCursor { get; init; }
    }

    private record GetEventResponse
    {
        public required EventDetail Event { get; init; }
        public object? Payload { get; init; }
        public bool Transient { get; init; }
        public List<EventDeliveryInfo>? Deliveries { get; init; }
    }
}

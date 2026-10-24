using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.EventTypes;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing event types for outbound webhooks.
/// </summary>
public class EventTypesResource : BaseResource
{
    public EventTypesResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List event types. Event types belong to the organization, not to an application: the API
    /// has no application filter and ignores <paramref name="applicationId"/>, so it is no longer
    /// sent. Use <see cref="ListAsync(EventTypeListOptions, CancellationToken)"/> to filter by
    /// category, enabled state or search text.
    /// </summary>
    public Task<CursorPage<EventType>> ListAsync(
        int limit = 50,
        string? cursor = null,
        string? applicationId = null,
        CancellationToken cancellationToken = default) =>
        ListAsync(new EventTypeListOptions { Limit = limit, Cursor = cursor }, cancellationToken);

    /// <summary>List event types with the filters the API supports.</summary>
    public async Task<CursorPage<EventType>> ListAsync(
        EventTypeListOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var queryParams = new Dictionary<string, string> { ["limit"] = options.Limit.ToString() };
        if (options.Cursor != null) queryParams["cursor"] = options.Cursor;
        if (options.Category != null) queryParams["category"] = options.Category;
        if (options.IsEnabled != null) queryParams["isEnabled"] = options.IsEnabled.Value ? "true" : "false";
        if (options.Search != null) queryParams["search"] = options.Search;

        var response = await ApiClient.RequestAsync<ListEventTypesResponse>(
            HttpMethod.Get,
            "/api/event-types",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new CursorPage<EventType>
        {
            Data = response.Data,
            HasMore = response.Pagination.HasMore,
            NextCursor = response.Pagination.NextCursor
        };
    }

    public async Task<EventType> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetEventTypeResponse>(
            HttpMethod.Get,
            $"/api/event-types/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    public async Task<EventType> CreateAsync(CreateEventTypeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetEventTypeResponse>(
            HttpMethod.Post,
            "/api/event-types",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    public async Task<EventType> UpdateAsync(string id, UpdateEventTypeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetEventTypeResponse>(
            HttpMethod.Patch,
            $"/api/event-types/{Uri.EscapeDataString(id)}",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Delete,
            $"/api/event-types/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    private record ListEventTypesResponse
    {
        public required List<EventType> Data { get; init; }
        public required CursorPaginationInfo Pagination { get; init; }
    }

    private record GetEventTypeResponse
    {
        public required EventType Data { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

/// <summary>Filters for <see cref="EventTypesResource.ListAsync(EventTypeListOptions, CancellationToken)"/>.</summary>
public record EventTypeListOptions
{
    /// <summary>Page size, at most 100. Default 50.</summary>
    public int Limit { get; init; } = 50;
    public string? Cursor { get; init; }
    public string? Category { get; init; }
    public bool? IsEnabled { get; init; }

    /// <summary>Matches the name, display name or description.</summary>
    public string? Search { get; init; }
}

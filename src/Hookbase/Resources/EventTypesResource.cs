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

    public async Task<CursorPage<EventType>> ListAsync(
        int limit = 50,
        string? cursor = null,
        string? applicationId = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string> { ["limit"] = limit.ToString() };
        if (cursor != null) queryParams["cursor"] = cursor;
        if (applicationId != null) queryParams["applicationId"] = applicationId;

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

using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Subscriptions;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing webhook subscriptions (endpoint + event type mappings).
/// </summary>
public class SubscriptionsResource : BaseResource
{
    public SubscriptionsResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List subscriptions with optional filtering.
    /// </summary>
    public async Task<CursorPage<Subscription>> ListAsync(
        int limit = 50,
        string? cursor = null,
        string? endpointId = null,
        string? eventTypeId = null,
        string? applicationId = null,
        bool? isEnabled = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string> { ["limit"] = limit.ToString() };
        if (cursor != null) queryParams["cursor"] = cursor;
        if (endpointId != null) queryParams["endpointId"] = endpointId;
        if (eventTypeId != null) queryParams["eventTypeId"] = eventTypeId;
        if (applicationId != null) queryParams["applicationId"] = applicationId;
        if (isEnabled.HasValue) queryParams["isEnabled"] = isEnabled.Value.ToString().ToLower();

        var response = await ApiClient.RequestAsync<ListSubscriptionsResponse>(
            HttpMethod.Get,
            "/api/webhook-subscriptions",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new CursorPage<Subscription>
        {
            Data = response.Data,
            HasMore = response.Pagination.HasMore,
            NextCursor = response.Pagination.NextCursor
        };
    }

    /// <summary>
    /// Get a single subscription by ID.
    /// </summary>
    public async Task<Subscription> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetSubscriptionResponse>(
            HttpMethod.Get,
            $"/api/webhook-subscriptions/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Create a new subscription.
    /// </summary>
    public async Task<Subscription> CreateAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetSubscriptionResponse>(
            HttpMethod.Post,
            "/api/webhook-subscriptions",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Update an existing subscription.
    /// </summary>
    public async Task<Subscription> UpdateAsync(string id, UpdateSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetSubscriptionResponse>(
            HttpMethod.Patch,
            $"/api/webhook-subscriptions/{Uri.EscapeDataString(id)}",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Delete a subscription.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Delete,
            $"/api/webhook-subscriptions/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Bulk subscribe an endpoint to multiple event types.
    /// </summary>
    public async Task<BulkSubscribeResult> BulkSubscribeAsync(BulkSubscribeRequest request, CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<BulkSubscribeResult>(
            HttpMethod.Post,
            "/api/webhook-subscriptions/bulk",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    // Internal response types
    private record ListSubscriptionsResponse
    {
        public required List<Subscription> Data { get; init; }
        public required CursorPaginationInfo Pagination { get; init; }
    }

    private record GetSubscriptionResponse
    {
        public required Subscription Data { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

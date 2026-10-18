using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Endpoints;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing customer webhook endpoints.
/// </summary>
public class EndpointsResource : BaseResource
{
    public EndpointsResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List endpoints with cursor-based pagination.
    /// </summary>
    public async Task<CursorPage<Endpoint>> ListAsync(
        string? applicationId = null,
        int limit = 50,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["limit"] = limit.ToString()
        };

        if (cursor != null) queryParams["cursor"] = cursor;
        if (applicationId != null) queryParams["applicationId"] = applicationId;

        var response = await ApiClient.RequestAsync<ListEndpointsResponse>(
            HttpMethod.Get,
            "/api/webhook-endpoints",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new CursorPage<Endpoint>
        {
            Data = response.Data,
            HasMore = response.Pagination.HasMore,
            NextCursor = response.Pagination.NextCursor
        };
    }

    /// <summary>
    /// Get an endpoint by ID.
    /// </summary>
    public async Task<Endpoint> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetEndpointResponse>(
            HttpMethod.Get,
            $"/api/webhook-endpoints/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Create a new endpoint.
    /// </summary>
    public async Task<EndpointWithSecret> CreateAsync(
        CreateEndpointRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<CreateEndpointResponse>(
            HttpMethod.Post,
            "/api/webhook-endpoints",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Update an existing endpoint.
    /// </summary>
    public async Task<Endpoint> UpdateAsync(
        string id,
        UpdateEndpointRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetEndpointResponse>(
            HttpMethod.Patch,
            $"/api/webhook-endpoints/{Uri.EscapeDataString(id)}",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Delete an endpoint.
    /// </summary>
    public async Task DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Delete,
            $"/api/webhook-endpoints/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Rotate the signing secret for an endpoint.
    /// </summary>
    public async Task<RotateSecretResult> RotateSecretAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<RotateSecretResult>(
            HttpMethod.Post,
            $"/api/webhook-endpoints/{Uri.EscapeDataString(id)}/rotate-secret",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Get delivery statistics for an endpoint.
    /// </summary>
    /// <remarks>
    /// The API has no <c>/webhook-endpoints/:id/stats</c> route, so this called a URL that always
    /// returned 404. The counters live on the endpoint itself; this reads the endpoint and returns them.
    /// </remarks>
    public async Task<EndpointStats> GetStatsAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await GetAsync(id, cancellationToken);

        return new EndpointStats
        {
            TotalMessages = endpoint.TotalMessages,
            TotalSuccesses = endpoint.TotalSuccesses,
            TotalFailures = endpoint.TotalFailures,
            SuccessRate = endpoint.TotalMessages == 0 ? 0 : (double)endpoint.TotalSuccesses / endpoint.TotalMessages,
            AverageLatency = endpoint.AvgResponseTimeMs ?? 0,
            RecentFailures = endpoint.CircuitFailureCount ?? 0
        };
    }

    // Internal response types
    private record ListEndpointsResponse
    {
        public required List<Endpoint> Data { get; init; }
        public required CursorPaginationInfo Pagination { get; init; }
    }

    private record GetEndpointResponse
    {
        public required Endpoint Data { get; init; }
    }

    private record CreateEndpointResponse
    {
        public required EndpointWithSecret Data { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

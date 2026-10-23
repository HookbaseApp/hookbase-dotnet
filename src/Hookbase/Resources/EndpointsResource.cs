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
    public Task<CursorPage<Endpoint>> ListAsync(
        string? applicationId = null,
        int limit = 50,
        string? cursor = null,
        CancellationToken cancellationToken = default) =>
        ListAsync(new EndpointListOptions { ApplicationId = applicationId, Limit = limit, Cursor = cursor }, cancellationToken);

    /// <summary>
    /// List endpoints with every filter the API supports.
    /// </summary>
    public async Task<CursorPage<Endpoint>> ListAsync(
        EndpointListOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var queryParams = new Dictionary<string, string>
        {
            ["limit"] = options.Limit.ToString()
        };

        if (options.Cursor != null) queryParams["cursor"] = options.Cursor;
        if (options.ApplicationId != null) queryParams["applicationId"] = options.ApplicationId;
        if (options.IsDisabled != null) queryParams["isDisabled"] = options.IsDisabled.Value ? "true" : "false";
        if (options.CircuitState != null) queryParams["circuitState"] = options.CircuitState;

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
    /// <remarks>The previous secret keeps signing for the API's default grace period of one hour.</remarks>
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
    /// Rotate the signing secret, keeping the previous one signing alongside it for
    /// <paramref name="gracePeriodSeconds"/> (0 to 86400; 0 retires it at once).
    /// </summary>
    public async Task<RotateSecretResult> RotateSecretAsync(
        string id,
        int gracePeriodSeconds,
        CancellationToken cancellationToken = default)
    {
        if (gracePeriodSeconds is < 0 or > 86400)
        {
            throw new ArgumentOutOfRangeException(nameof(gracePeriodSeconds), gracePeriodSeconds, "Must be between 0 and 86400 seconds");
        }

        return await ApiClient.RequestAsync<RotateSecretResult>(
            HttpMethod.Post,
            $"/api/webhook-endpoints/{Uri.EscapeDataString(id)}/rotate-secret",
            body: new { gracePeriodSeconds },
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

/// <summary>Filters for <see cref="EndpointsResource.ListAsync(EndpointListOptions, CancellationToken)"/>.</summary>
public record EndpointListOptions
{
    public string? ApplicationId { get; init; }

    /// <summary>Page size, at most 100. Default 50.</summary>
    public int Limit { get; init; } = 50;
    public string? Cursor { get; init; }
    public bool? IsDisabled { get; init; }

    /// <summary><c>closed</c>, <c>open</c> or <c>half_open</c>.</summary>
    public string? CircuitState { get; init; }
}

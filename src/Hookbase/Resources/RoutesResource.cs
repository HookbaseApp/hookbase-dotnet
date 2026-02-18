using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Routes;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing webhook routes with circuit breaker support.
/// </summary>
public class RoutesResource : BaseResource
{
    public RoutesResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List routes with offset-based pagination.
    /// </summary>
    public async Task<OffsetPage<Route>> ListAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        var response = await ApiClient.RequestAsync<ListRoutesResponse>(
            HttpMethod.Get,
            "/api/routes",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new OffsetPage<Route>
        {
            Data = response.Routes,
            Total = response.Pagination.Total,
            Page = response.Pagination.Page,
            PageSize = response.Pagination.PageSize
        };
    }

    /// <summary>
    /// Get a single route by ID.
    /// </summary>
    public async Task<Route> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetRouteResponse>(
            HttpMethod.Get,
            $"/api/routes/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Route;
    }

    /// <summary>
    /// Create a new route.
    /// </summary>
    public async Task<Route> CreateAsync(CreateRouteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetRouteResponse>(
            HttpMethod.Post,
            "/api/routes",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Route;
    }

    /// <summary>
    /// Update an existing route.
    /// </summary>
    public async Task UpdateAsync(string id, UpdateRouteRequest request, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Patch,
            $"/api/routes/{Uri.EscapeDataString(id)}",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Delete a route.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Delete,
            $"/api/routes/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Bulk delete multiple routes.
    /// </summary>
    public async Task<BulkDeleteResult> BulkDeleteAsync(BulkDeleteRoutesRequest request, CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<BulkDeleteResult>(
            HttpMethod.Delete,
            "/api/routes/bulk",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Bulk update routes (pause/resume).
    /// </summary>
    public async Task<BulkUpdateResult> BulkUpdateAsync(BulkUpdateRoutesRequest request, CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<BulkUpdateResult>(
            HttpMethod.Patch,
            "/api/routes/bulk",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Export routes as JSON.
    /// </summary>
    public async Task<RouteExport> ExportAsync(CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<RouteExport>(
            HttpMethod.Get,
            "/api/routes/export",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Import routes from JSON.
    /// </summary>
    public async Task<ImportRoutesResult> ImportAsync(ImportRoutesRequest request, CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<ImportRoutesResult>(
            HttpMethod.Post,
            "/api/routes/import",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Get circuit breaker status for a route.
    /// </summary>
    public async Task<CircuitStatusResponse> GetCircuitStatusAsync(string id, CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<CircuitStatusResponse>(
            HttpMethod.Get,
            $"/api/routes/{Uri.EscapeDataString(id)}/circuit-status",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Reset circuit breaker for a route.
    /// </summary>
    public async Task<CircuitResetResult> ResetCircuitAsync(string id, CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<CircuitResetResult>(
            HttpMethod.Post,
            $"/api/routes/{Uri.EscapeDataString(id)}/reset-circuit",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Update circuit breaker configuration for a route.
    /// </summary>
    public async Task<Route> UpdateCircuitConfigAsync(string id, UpdateCircuitConfigRequest request, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetRouteResponse>(
            HttpMethod.Patch,
            $"/api/routes/{Uri.EscapeDataString(id)}/circuit-config",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Route;
    }

    // Internal response types
    private record ListRoutesResponse
    {
        public required List<Route> Routes { get; init; }
        public required PaginationInfo Pagination { get; init; }
    }

    private record GetRouteResponse
    {
        public required Route Route { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

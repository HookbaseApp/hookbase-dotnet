using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Filters;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing webhook content filters.
/// </summary>
public class FiltersResource : BaseResource
{
    public FiltersResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List all filters with offset-based pagination.
    /// </summary>
    public async Task<OffsetPage<Filter>> ListAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        var response = await ApiClient.RequestAsync<ListFiltersResponse>(
            HttpMethod.Get,
            "/api/filters",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new OffsetPage<Filter>
        {
            Data = response.Filters,
            Total = response.Pagination.Total,
            Page = response.Pagination.Page,
            PageSize = response.Pagination.PageSize
        };
    }

    /// <summary>
    /// Get a filter by ID.
    /// </summary>
    public async Task<Filter> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetFilterResponse>(
            HttpMethod.Get,
            $"/api/filters/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Filter;
    }

    /// <summary>
    /// Create a new filter.
    /// </summary>
    public async Task<Filter> CreateAsync(
        CreateFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetFilterResponse>(
            HttpMethod.Post,
            "/api/filters",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Filter;
    }

    /// <summary>
    /// Update an existing filter.
    /// </summary>
    public async Task UpdateAsync(
        string id,
        UpdateFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Patch,
            $"/api/filters/{Uri.EscapeDataString(id)}",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Delete a filter.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Delete,
            $"/api/filters/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Test filter evaluation against sample payload.
    /// </summary>
    public async Task<TestFilterResult> TestAsync(
        TestFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<TestFilterResult>(
            HttpMethod.Post,
            "/api/filters/test",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    // Internal response types
    private record ListFiltersResponse
    {
        public required List<Filter> Filters { get; init; }
        public required PaginationInfo Pagination { get; init; }
    }

    private record GetFilterResponse
    {
        public required Filter Filter { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

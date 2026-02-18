using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Destinations;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing webhook delivery destinations.
/// </summary>
public class DestinationsResource : BaseResource
{
    public DestinationsResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List all destinations with offset-based pagination.
    /// </summary>
    public async Task<OffsetPage<Destination>> ListAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        var response = await ApiClient.RequestAsync<ListDestinationsResponse>(
            HttpMethod.Get,
            "/api/destinations",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new OffsetPage<Destination>
        {
            Data = response.Destinations,
            Total = response.Pagination.Total,
            Page = response.Pagination.Page,
            PageSize = response.Pagination.PageSize
        };
    }

    /// <summary>
    /// Iterate through all destinations with automatic pagination.
    /// </summary>
    public async IAsyncEnumerable<Destination> ListAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int page = 1;
        bool hasMore = true;

        while (hasMore)
        {
            var result = await ListAsync(page, 100, cancellationToken);
            foreach (var item in result.Data)
            {
                yield return item;
            }
            hasMore = result.HasMore;
            page++;
        }
    }

    /// <summary>
    /// Get a destination by ID or slug.
    /// </summary>
    public async Task<Destination> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetDestinationResponse>(
            HttpMethod.Get,
            $"/api/destinations/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Destination;
    }

    /// <summary>
    /// Create a new destination.
    /// </summary>
    public async Task<Destination> CreateAsync(
        CreateDestinationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetDestinationResponse>(
            HttpMethod.Post,
            "/api/destinations",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Destination;
    }

    /// <summary>
    /// Update an existing destination.
    /// </summary>
    public async Task UpdateAsync(
        string id,
        UpdateDestinationRequest request,
        CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Patch,
            $"/api/destinations/{Uri.EscapeDataString(id)}",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Delete a destination.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Delete,
            $"/api/destinations/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Delete multiple destinations in bulk.
    /// </summary>
    public async Task<BulkDeleteResult> BulkDeleteAsync(
        List<string> ids,
        CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<BulkDeleteResult>(
            HttpMethod.Delete,
            "/api/destinations/bulk",
            body: new BulkDeleteRequest { Ids = ids },
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Test destination connectivity by sending a test webhook.
    /// </summary>
    public async Task<TestDestinationResult> TestAsync(string id, CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<TestDestinationResult>(
            HttpMethod.Post,
            $"/api/destinations/{Uri.EscapeDataString(id)}/test",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Export destinations to JSON.
    /// </summary>
    public async Task<DestinationExport> ExportAsync(
        List<string>? ids = null,
        bool includeSensitive = false,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["includeSensitive"] = includeSensitive.ToString().ToLower()
        };

        if (ids != null && ids.Count > 0)
        {
            queryParams["ids"] = string.Join(",", ids);
        }

        return await ApiClient.RequestAsync<DestinationExport>(
            HttpMethod.Get,
            "/api/destinations/export",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Import destinations from JSON.
    /// </summary>
    public async Task<ImportDestinationsResult> ImportAsync(
        ImportDestinationsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<ImportDestinationsResult>(
            HttpMethod.Post,
            "/api/destinations/import",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    // Internal response types
    private record ListDestinationsResponse
    {
        public required List<Destination> Destinations { get; init; }
        public required PaginationInfo Pagination { get; init; }
    }

    private record GetDestinationResponse
    {
        public required Destination Destination { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

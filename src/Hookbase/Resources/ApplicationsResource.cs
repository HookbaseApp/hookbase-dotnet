using Hookbase.Http;
using Hookbase.Models.Applications;
using Hookbase.Models.Common;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing customer webhook applications.
/// </summary>
public class ApplicationsResource : BaseResource
{
    public ApplicationsResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List all applications with cursor-based pagination.
    /// </summary>
    public async Task<CursorPage<Application>> ListAsync(
        int limit = 50,
        string? cursor = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["limit"] = limit.ToString()
        };

        if (cursor != null) queryParams["cursor"] = cursor;
        if (search != null) queryParams["search"] = search;

        var response = await ApiClient.RequestAsync<ListApplicationsResponse>(
            HttpMethod.Get,
            "/api/webhook-applications",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new CursorPage<Application>
        {
            Data = response.Data,
            HasMore = response.Pagination.HasMore,
            NextCursor = response.Pagination.NextCursor
        };
    }

    /// <summary>
    /// Iterate through all applications with automatic pagination.
    /// </summary>
    public async IAsyncEnumerable<Application> ListAllAsync(
        string? search = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? cursor = null;
        bool hasMore = true;

        while (hasMore)
        {
            var result = await ListAsync(100, cursor, search, cancellationToken);
            foreach (var item in result.Data)
            {
                yield return item;
            }
            hasMore = result.HasMore;
            cursor = result.NextCursor;
        }
    }

    /// <summary>
    /// Get an application by ID.
    /// </summary>
    public async Task<Application> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetApplicationResponse>(
            HttpMethod.Get,
            $"/api/webhook-applications/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Create a new application.
    /// </summary>
    public async Task<Application> CreateAsync(
        CreateApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetApplicationResponse>(
            HttpMethod.Post,
            "/api/webhook-applications",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Update an existing application.
    /// </summary>
    public async Task<Application> UpdateAsync(
        string id,
        UpdateApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetApplicationResponse>(
            HttpMethod.Patch,
            $"/api/webhook-applications/{Uri.EscapeDataString(id)}",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// Delete an application.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<object>(
            HttpMethod.Delete,
            $"/api/webhook-applications/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Get or create an application by external ID (upsert operation).
    /// </summary>
    public async Task<Application> GetOrCreateAsync(
        GetOrCreateApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetApplicationResponse>(
            HttpMethod.Put,
            "/api/webhook-applications/upsert",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    // Internal response types
    private record ListApplicationsResponse
    {
        public required List<Application> Data { get; init; }
        public required CursorPaginationInfo Pagination { get; init; }
    }

    private record GetApplicationResponse
    {
        public required Application Data { get; init; }
    }
}

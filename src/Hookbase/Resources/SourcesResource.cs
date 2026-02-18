using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Sources;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing inbound webhook sources.
/// </summary>
public class SourcesResource : BaseResource
{
    public SourcesResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List all sources with offset-based pagination.
    /// </summary>
    public async Task<OffsetPage<Source>> ListAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        if (search != null) queryParams["search"] = search;
        if (isActive.HasValue) queryParams["isActive"] = isActive.Value.ToString().ToLower();

        var response = await ApiClient.RequestAsync<ListSourcesResponse>(
            HttpMethod.Get,
            "/api/sources",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new OffsetPage<Source>
        {
            Data = response.Sources,
            Total = response.Pagination.Total,
            Page = response.Pagination.Page,
            PageSize = response.Pagination.PageSize
        };
    }

    /// <summary>
    /// Iterate through all sources with automatic pagination.
    /// </summary>
    public async IAsyncEnumerable<Source> ListAllAsync(
        string? search = null,
        bool? isActive = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int page = 1;
        bool hasMore = true;

        while (hasMore)
        {
            var result = await ListAsync(page, 100, search, isActive, cancellationToken);
            foreach (var item in result.Data)
            {
                yield return item;
            }
            hasMore = result.HasMore;
            page++;
        }
    }

    /// <summary>
    /// Get a source by ID or slug.
    /// </summary>
    public async Task<Source> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetSourceResponse>(
            HttpMethod.Get,
            $"/api/sources/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Source;
    }

    /// <summary>
    /// Create a new source.
    /// </summary>
    public async Task<SourceWithSecret> CreateAsync(
        CreateSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<CreateSourceResponse>(
            HttpMethod.Post,
            "/api/sources",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Source;
    }

    /// <summary>
    /// Update an existing source.
    /// </summary>
    public async Task UpdateAsync(
        string id,
        UpdateSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Patch,
            $"/api/sources/{Uri.EscapeDataString(id)}",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Delete a source.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<object>(
            HttpMethod.Delete,
            $"/api/sources/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Rotate the signing secret for a source.
    /// </summary>
    public async Task<string> RotateSecretAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<RotateSecretResponse>(
            HttpMethod.Post,
            $"/api/sources/{Uri.EscapeDataString(id)}/rotate-secret",
            cancellationToken: cancellationToken
        );

        return response.SigningSecret;
    }

    /// <summary>
    /// Reveal the signing secret for a source.
    /// </summary>
    public async Task<string> RevealSecretAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<RevealSecretResponse>(
            HttpMethod.Get,
            $"/api/sources/{Uri.EscapeDataString(id)}/reveal-secret",
            cancellationToken: cancellationToken
        );

        return response.SigningSecret;
    }

    /// <summary>
    /// Export sources to JSON.
    /// </summary>
    public async Task<SourceExport> ExportAsync(CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<SourceExport>(
            HttpMethod.Get,
            "/api/sources/export",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Import sources from JSON.
    /// </summary>
    public async Task<ImportSourcesResult> ImportAsync(
        List<CreateSourceRequest> sources,
        CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<ImportSourcesResult>(
            HttpMethod.Post,
            "/api/sources/import",
            body: new { sources },
            cancellationToken: cancellationToken
        );
    }

    // Internal response types
    private record ListSourcesResponse
    {
        public required List<Source> Sources { get; init; }
        public required PaginationInfo Pagination { get; init; }
    }

    private record GetSourceResponse
    {
        public required Source Source { get; init; }
    }

    private record CreateSourceResponse
    {
        public required SourceWithSecret Source { get; init; }
    }

    private record RotateSecretResponse
    {
        public required string SigningSecret { get; init; }
    }

    private record RevealSecretResponse
    {
        public required string SigningSecret { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }

}

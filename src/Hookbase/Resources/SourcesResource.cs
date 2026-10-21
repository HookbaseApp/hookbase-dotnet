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
    /// Get a source by ID or slug. An unknown ID or slug throws <see cref="Hookbase.Exceptions.NotFoundException"/>.
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
    /// Update an existing source. The API answers <c>{success: true}</c>, not the updated source;
    /// call <see cref="GetAsync"/> to read it back. See <see cref="UpdateSourceRequest.Clear"/>
    /// to send an explicit null.
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
    /// Replace the signing secret with a freshly generated one and return it.
    /// </summary>
    /// <remarks>
    /// Only applies where Hookbase chooses the secret. The API answers 400
    /// (<see cref="Hookbase.Exceptions.ValidationException"/>) for providers whose key is built in
    /// (wise, aws-sns), that verify with a public key (paste the vendor's new key with
    /// <see cref="UpdateAsync"/> instead), that issue their own secret (standard-webhooks and
    /// friends: regenerate it at the vendor, then update), and whose secret arrives after
    /// registration (asana, notion).
    /// </remarks>
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
    /// Reveal the signing secret for a source. A source with no secret throws
    /// <see cref="Hookbase.Exceptions.NotFoundException"/>.
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
    /// Get the Meta verify token for a source (whatsapp). A provider with no verify token throws
    /// <see cref="Hookbase.Exceptions.ValidationException"/>.
    /// </summary>
    public async Task<string> GetVerifyTokenAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<VerifyTokenResponse>(
            HttpMethod.Get,
            $"/api/sources/{Uri.EscapeDataString(id)}/verify-token",
            cancellationToken: cancellationToken
        );

        return response.VerifyToken;
    }

    /// <summary>
    /// Generate a new Meta verify token for a source and return it. Meta checks the token only when
    /// the callback URL is (re)registered, so paste the new one there.
    /// </summary>
    public async Task<string> RotateVerifyTokenAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<VerifyTokenResponse>(
            HttpMethod.Post,
            $"/api/sources/{Uri.EscapeDataString(id)}/rotate-verify-token",
            cancellationToken: cancellationToken
        );

        return response.VerifyToken;
    }

    /// <summary>
    /// Get the verification token a vendor delivered to the ingest URL during registration
    /// (notion). Throws <see cref="Hookbase.Exceptions.NotFoundException"/> when none is waiting.
    /// </summary>
    /// <remarks>
    /// The token arrived unauthenticated: paste it into the vendor's verify form first, and only
    /// then call <see cref="AdoptPendingVerificationAsync"/>. A planted token simply fails there.
    /// </remarks>
    public async Task<PendingVerification> GetPendingVerificationAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<PendingVerification>(
            HttpMethod.Get,
            $"/api/sources/{Uri.EscapeDataString(id)}/pending-verification",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Make the waiting verification token the source's signing secret. Throws
    /// <see cref="Hookbase.Exceptions.NotFoundException"/> when none is waiting.
    /// </summary>
    public async Task AdoptPendingVerificationAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Post,
            $"/api/sources/{Uri.EscapeDataString(id)}/pending-verification/adopt",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Delete up to 100 sources. If any ID does not belong to the organization nothing is deleted
    /// and the API answers 400 (<see cref="Hookbase.Exceptions.ValidationException"/>).
    /// </summary>
    /// <returns>The number of sources deleted.</returns>
    public async Task<int> BulkDeleteAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<BulkDeleteResponse>(
            HttpMethod.Delete,
            "/api/sources/bulk",
            body: new { ids = ids.ToList() },
            cancellationToken: cancellationToken
        );

        return response.Deleted;
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
    /// Import sources from JSON, skipping any whose slug already exists.
    /// </summary>
    public Task<ImportSourcesResult> ImportAsync(
        List<CreateSourceRequest> sources,
        CancellationToken cancellationToken = default)
    {
        return ImportAsync(sources, "skip", null, cancellationToken);
    }

    /// <summary>
    /// Import sources from JSON.
    /// </summary>
    /// <param name="sources">The sources to import.</param>
    /// <param name="conflictStrategy">What to do when a slug already exists: <c>skip</c>, <c>rename</c> or <c>overwrite</c>.</param>
    /// <param name="validateOnly">Report what would happen without importing anything.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ImportSourcesResult> ImportAsync(
        List<CreateSourceRequest> sources,
        string conflictStrategy,
        bool? validateOnly = null,
        CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<ImportSourcesResult>(
            HttpMethod.Post,
            "/api/sources/import",
            body: new ImportSourcesRequest
            {
                Sources = sources,
                ConflictStrategy = conflictStrategy,
                ValidateOnly = validateOnly
            },
            cancellationToken: cancellationToken
        );
    }

    // Internal request/response types
    private record ImportSourcesRequest
    {
        public required List<CreateSourceRequest> Sources { get; init; }
        public required string ConflictStrategy { get; init; }
        public bool? ValidateOnly { get; init; }
    }

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

    private record VerifyTokenResponse
    {
        public required string VerifyToken { get; init; }
    }

    private record BulkDeleteResponse
    {
        public bool Success { get; init; }
        public int Deleted { get; init; }
    }

}

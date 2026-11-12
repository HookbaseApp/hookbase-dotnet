using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Destinations;
using Hookbase.Pagination;
using System.Text.Json;

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
    /// <remarks>
    /// For an HTTP destination the result carries <see cref="Destination.SigningSecret"/>, the
    /// secret its deliveries are signed with. The API shows it this once; read it again with
    /// <see cref="RevealSecretAsync"/>.
    /// </remarks>
    public async Task<Destination> CreateAsync(
        CreateDestinationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Resolve the slug up front: the API requires it, and an underivable name has to fail here
        // rather than as a 400 from a request we should never have sent.
        var response = await ApiClient.RequestAsync<GetDestinationResponse>(
            HttpMethod.Post,
            "/api/destinations",
            body: request.WithResolvedSlug(),
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
    /// <remarks>
    /// A bridge destination is dry-run instead, with the API's own sample payload; pass a payload
    /// with <see cref="TestAsync(string, JsonElement?, CancellationToken)"/>.
    /// </remarks>
    public async Task<TestDestinationResult> TestAsync(string id, CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<TestDestinationResult>(
            HttpMethod.Post,
            $"/api/destinations/{Uri.EscapeDataString(id)}/test",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Test a destination with a sample payload (<c>POST /api/destinations/:id/test</c>).
    /// </summary>
    /// <remarks>
    /// The payload matters for a bridge destination: the test is a dry run that resolves the event
    /// type from <paramref name="payload"/> (a JSON object) and counts the endpoints subscribed to
    /// it (<see cref="TestDestinationResult.DryRun"/>, <see cref="TestDestinationResult.EventType"/>,
    /// <see cref="TestDestinationResult.MatchingEndpoints"/>). Nothing is published or charged.
    /// Other destination types ignore it and send their usual test request. A null payload sends no
    /// body, the same as <see cref="TestAsync(string, CancellationToken)"/>.
    /// </remarks>
    public async Task<TestDestinationResult> TestAsync(
        string id,
        JsonElement? payload,
        CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<TestDestinationResult>(
            HttpMethod.Post,
            $"/api/destinations/{Uri.EscapeDataString(id)}/test",
            body: payload is { } sample ? new Dictionary<string, object> { ["payload"] = sample } : null,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Read an HTTP destination's signing secret (<c>GET /api/destinations/:id/reveal-secret</c>).
    /// </summary>
    /// <remarks>
    /// Needs a member role or higher and, for an API key, the <c>write</c> scope; the API audit-logs
    /// each reveal. A destination that does not sign (queue, storage, bridge) is a 400.
    /// </remarks>
    public async Task<DestinationSecret> RevealSecretAsync(string id, CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<DestinationSecret>(
            HttpMethod.Get,
            $"/api/destinations/{Uri.EscapeDataString(id)}/reveal-secret",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Issue a new signing secret for an HTTP destination (<c>POST /api/destinations/:id/rotate-secret</c>).
    /// </summary>
    /// <param name="id">Destination ID or slug.</param>
    /// <param name="gracePeriodSeconds">
    /// How long deliveries keep carrying a signature made with the previous secret alongside the new
    /// one, 0 to 86400. Null uses the API default (3600); 0 retires the previous secret at once.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DestinationSecret> RotateSecretAsync(
        string id,
        int? gracePeriodSeconds = null,
        CancellationToken cancellationToken = default)
    {
        if (gracePeriodSeconds is < 0 or > 86400)
        {
            throw new ArgumentOutOfRangeException(nameof(gracePeriodSeconds), gracePeriodSeconds, "Must be between 0 and 86400 seconds");
        }

        return await ApiClient.RequestAsync<DestinationSecret>(
            HttpMethod.Post,
            $"/api/destinations/{Uri.EscapeDataString(id)}/rotate-secret",
            body: gracePeriodSeconds is { } grace ? new Dictionary<string, object> { ["gracePeriodSeconds"] = grace } : null,
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
        var resolved = request with
        {
            Destinations = request.Destinations.Select(destination => destination.WithResolvedSlug()).ToList()
        };

        return await ApiClient.RequestAsync<ImportDestinationsResult>(
            HttpMethod.Post,
            "/api/destinations/import",
            body: resolved,
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

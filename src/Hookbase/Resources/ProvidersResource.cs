using Hookbase.Http;
using Hookbase.Models.Providers;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for the public provider catalog: which providers Hookbase knows, how each one
/// signs, and what goes in a source's signing secret for it.
/// </summary>
/// <remarks>
/// The catalog endpoints need no authentication; the client's key is sent anyway and ignored.
/// </remarks>
public class ProvidersResource : BaseResource
{
    public ProvidersResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List catalog providers.
    /// </summary>
    /// <param name="category">Exact category match, e.g. <c>payments</c>.</param>
    /// <param name="search">Case-insensitive substring match on name, description and category.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ProviderList> ListAsync(
        string? category = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        if (category != null) queryParams["category"] = category;
        if (search != null) queryParams["search"] = search;

        return await ApiClient.RequestAsync<ProviderList>(
            HttpMethod.Get,
            "/api/catalog/providers",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Get one provider's full catalog entry by catalog id. An unknown id throws
    /// <see cref="Hookbase.Exceptions.NotFoundException"/>.
    /// </summary>
    public async Task<Provider> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetProviderResponse>(
            HttpMethod.Get,
            $"/api/catalog/providers/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Provider;
    }

    /// <summary>
    /// Get one provider's event types and sample payloads. An unknown id throws
    /// <see cref="Hookbase.Exceptions.NotFoundException"/>.
    /// </summary>
    public async Task<ProviderEvents> GetEventsAsync(string id, CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<ProviderEvents>(
            HttpMethod.Get,
            $"/api/catalog/providers/{Uri.EscapeDataString(id)}/events",
            cancellationToken: cancellationToken
        );
    }

    private record GetProviderResponse
    {
        public required Provider Provider { get; init; }
    }
}

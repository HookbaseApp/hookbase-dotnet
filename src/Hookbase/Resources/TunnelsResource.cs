using Hookbase.Http;
using Hookbase.Models.Destinations;
using Hookbase.Models.Tunnels;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing webhook tunnels for local development.
/// </summary>
public class TunnelsResource : BaseResource
{
    public TunnelsResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List all tunnels.
    /// </summary>
    public async Task<List<Tunnel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<ListTunnelsResponse>(
            HttpMethod.Get,
            "/api/tunnels",
            cancellationToken: cancellationToken
        );

        return response.Tunnels;
    }

    /// <summary>
    /// Get a tunnel by ID.
    /// </summary>
    public async Task<Tunnel> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetTunnelResponse>(
            HttpMethod.Get,
            $"/api/tunnels/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Tunnel;
    }

    /// <summary>
    /// Create a new tunnel.
    /// </summary>
    public async Task<Tunnel> CreateAsync(
        CreateTunnelRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetTunnelResponse>(
            HttpMethod.Post,
            "/api/tunnels",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Tunnel;
    }

    /// <summary>
    /// Bulk delete tunnels.
    /// </summary>
    public async Task<BulkDeleteResult> BulkDeleteAsync(
        List<string> ids,
        CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<BulkDeleteResult>(
            HttpMethod.Delete,
            "/api/tunnels/bulk",
            body: new BulkDeleteRequest { Ids = ids },
            cancellationToken: cancellationToken
        );
    }

    // Internal response types
    private record ListTunnelsResponse
    {
        public required List<Tunnel> Tunnels { get; init; }
    }

    private record GetTunnelResponse
    {
        public required Tunnel Tunnel { get; init; }
    }
}

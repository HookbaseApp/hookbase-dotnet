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
    /// Create a new tunnel. The result carries <see cref="Tunnel.AuthToken"/>,
    /// <see cref="Tunnel.TunnelUrl"/> and <see cref="Tunnel.WsUrl"/>, which only this call returns.
    /// </summary>
    public async Task<Tunnel> CreateAsync(
        CreateTunnelRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<CreateTunnelResponse>(
            HttpMethod.Post,
            "/api/tunnels",
            body: request,
            cancellationToken: cancellationToken
        );

        // tunnelUrl and wsUrl sit beside the tunnel, not in it.
        return response.Tunnel with
        {
            TunnelUrl = response.Tunnel.TunnelUrl ?? response.TunnelUrl,
            WsUrl = response.Tunnel.WsUrl ?? response.WsUrl,
        };
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

    private record CreateTunnelResponse
    {
        public required Tunnel Tunnel { get; init; }
        public string? TunnelUrl { get; init; }
        public string? WsUrl { get; init; }
    }

    private record GetTunnelResponse
    {
        public required Tunnel Tunnel { get; init; }
    }
}

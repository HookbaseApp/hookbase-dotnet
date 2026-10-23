using System.Text.Json.Serialization;
using Hookbase.Json;

namespace Hookbase.Models.Tunnels;

/// <summary>
/// Tunnel for local development webhook forwarding.
/// </summary>
public record Tunnel
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? UserId { get; init; }
    public string? Name { get; init; }
    public string? Subdomain { get; init; }
    public int? LocalPort { get; init; }
    public string? Status { get; init; }

    /// <summary><c>inbound</c> or <c>bidirectional</c>.</summary>
    public string? Direction { get; init; }

    /// <summary>Hosts a bidirectional tunnel may reach (sent by the API as JSON text; both forms are read).</summary>
    [JsonConverter(typeof(JsonTextOrValueConverter<List<string>>))]
    public List<string>? AllowedHosts { get; init; }

    public string? CustomDomainId { get; init; }
    public int? TotalRequests { get; init; }
    public string? LastConnectedAt { get; init; }
    public string? LastRequestAt { get; init; }

    /// <summary>Public URL of the tunnel. Set by <c>CreateAsync</c> only; list and get do not return it.</summary>
    public string? TunnelUrl { get; init; }

    /// <summary>WebSocket URL (with the auth token) the CLI connects to. Set by <c>CreateAsync</c> only.</summary>
    public string? WsUrl { get; init; }

    /// <summary>
    /// The tunnel's auth token. Returned once, by <c>CreateAsync</c>; the API stores only its hash,
    /// so list and get never return it. Treat it as a secret.
    /// </summary>
    [JsonPropertyName("auth_token")]
    public string? AuthToken { get; init; }

    public string? CreatedAt { get; init; }

    [Obsolete("The API does not return updatedAt for tunnels; it is always null.")]
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Input for creating a new tunnel.
/// </summary>
public record CreateTunnelRequest
{
    public required string Name { get; init; }
    public string? Subdomain { get; init; }

    /// <summary><c>inbound</c> (API default) or <c>bidirectional</c>.</summary>
    public string? Direction { get; init; }

    /// <summary>Hosts a bidirectional tunnel may reach.</summary>
    public List<string>? AllowedHosts { get; init; }
}

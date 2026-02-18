namespace Hookbase.Models.Tunnels;

/// <summary>
/// Tunnel for local development webhook forwarding.
/// </summary>
public record Tunnel
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? Subdomain { get; init; }
    public string? Status { get; init; }
    public string? TunnelUrl { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Input for creating a new tunnel.
/// </summary>
public record CreateTunnelRequest
{
    public required string Name { get; init; }
    public string? Subdomain { get; init; }
}

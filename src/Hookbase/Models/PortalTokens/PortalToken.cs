using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.PortalTokens;

/// <summary>
/// Portal access token for customer self-service.
/// </summary>
public record PortalToken
{
    public string? Id { get; init; }
    public string? ApplicationId { get; init; }
    public string? Token { get; init; }
    public string? TokenPrefix { get; init; }
    public string? Name { get; init; }
    public List<string> Scopes { get; init; } = new();
    public string? ExpiresAt { get; init; }
    public string? CreatedAt { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsExpired { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsRevoked { get; init; }

    public int? UseCount { get; init; }
    public string? LastUsedAt { get; init; }
    public string? RevokedAt { get; init; }
}

/// <summary>
/// Input for creating a new portal token.
/// </summary>
public record CreatePortalTokenRequest
{
    public string? Name { get; init; }
    public List<string>? Scopes { get; init; }
    public int? ExpiresInDays { get; init; }
    public List<string>? AllowedIps { get; init; }
}

using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.ApiKeys;

/// <summary>
/// API key for authentication.
/// </summary>
public record ApiKey
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? KeyPrefix { get; init; }
    public object? Scopes { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsDisabled { get; init; }

    public string? LastUsedAt { get; init; }
    public string? ExpiresAt { get; init; }
    public string? CreatedAt { get; init; }
}

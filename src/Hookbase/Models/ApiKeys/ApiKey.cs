using Hookbase.Json;
using System.Text.Json;
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
    /// <summary>The scopes as the API sent them (JSON text such as <c>["read","write"]</c>). See <see cref="ScopeList"/>.</summary>
    public object? Scopes { get; init; }

    /// <summary><see cref="Scopes"/> parsed into a list, whether the API sent JSON text or an array.</summary>
    [JsonIgnore]
    public List<string> ScopeList
    {
        get
        {
            try
            {
                return Scopes switch
                {
                    JsonElement { ValueKind: JsonValueKind.String } text when !string.IsNullOrWhiteSpace(text.GetString()) =>
                        JsonSerializer.Deserialize<List<string>>(text.GetString()!) ?? new(),
                    JsonElement { ValueKind: JsonValueKind.Array } array =>
                        array.EnumerateArray().Select(e => e.ToString()).ToList(),
                    string s when !string.IsNullOrWhiteSpace(s) => JsonSerializer.Deserialize<List<string>>(s) ?? new(),
                    IEnumerable<string> list => list.ToList(),
                    _ => new(),
                };
            }
            catch (JsonException)
            {
                return new();
            }
        }
    }

    [Obsolete("The API does not return isDisabled for API keys; it is always false.")]
    [JsonConverter(typeof(BooleanConverter))]
    public bool IsDisabled { get; init; }

    public string? LastUsedAt { get; init; }
    public string? ExpiresAt { get; init; }
    public string? CreatedAt { get; init; }
}

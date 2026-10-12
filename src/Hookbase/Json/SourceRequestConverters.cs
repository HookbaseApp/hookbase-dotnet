using Hookbase.Models.Sources;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Json;

/// <summary>
/// The canonical wire body for <c>PATCH /api/sources/:id</c>.
/// </summary>
/// <remarks>
/// Every key is spelled explicitly and omitted when null, so the body is identical whichever
/// <see cref="JsonSerializerOptions"/> the caller uses. <c>updateSourceSchema</c> is strict: a key
/// not listed here is a 400.
/// </remarks>
internal sealed class SourceUpdatePayload
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Provider { get; set; }

    [JsonPropertyName("isActive")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsActive { get; set; }

    [JsonPropertyName("signingSecret")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SigningSecret { get; set; }

    [JsonPropertyName("verifyToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VerifyToken { get; set; }

    [JsonPropertyName("rejectInvalidSignatures")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RejectInvalidSignatures { get; set; }

    [JsonPropertyName("rateLimitPerMinute")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RateLimitPerMinute { get; set; }

    [JsonPropertyName("ipFilterMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IpFilterMode { get; set; }

    [JsonPropertyName("ipAllowlist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? IpAllowlist { get; set; }

    [JsonPropertyName("ipDenylist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? IpDenylist { get; set; }

    [JsonPropertyName("encryptFields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? EncryptFields { get; set; }

    [JsonPropertyName("maskFields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? MaskFields { get; set; }

    [JsonPropertyName("dedupEnabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DedupEnabled { get; set; }

    [JsonPropertyName("dedupStrategy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DedupStrategy { get; set; }

    [JsonPropertyName("dedupWindowHours")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DedupWindowHours { get; set; }

    [JsonPropertyName("dedupCustomHeader")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DedupCustomHeader { get; set; }

    [JsonPropertyName("transientMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? TransientMode { get; set; }

    [JsonPropertyName("allowedMethods")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedMethods { get; set; }

    internal static SourceUpdatePayload From(UpdateSourceRequest request) => new()
    {
        Name = request.Name,
        Description = request.Description,
        Provider = request.Provider,
        IsActive = request.IsActive,
        SigningSecret = request.SigningSecret,
        VerifyToken = request.VerifyToken,
        RejectInvalidSignatures = request.RejectInvalidSignatures,
        RateLimitPerMinute = request.RateLimitPerMinute,
        IpFilterMode = request.IpFilterMode,
        IpAllowlist = request.IpAllowlist,
        IpDenylist = request.IpDenylist,
        EncryptFields = request.EncryptFields,
        MaskFields = request.MaskFields,
        DedupEnabled = request.DedupEnabled,
        DedupStrategy = request.DedupStrategy,
        DedupWindowHours = request.DedupWindowHours,
        DedupCustomHeader = request.DedupCustomHeader,
        TransientMode = request.TransientMode,
        AllowedMethods = request.AllowedMethods
    };

    internal UpdateSourceRequest ToRequest() => new()
    {
        Name = Name,
        Description = Description,
        Provider = Provider,
        IsActive = IsActive,
        SigningSecret = SigningSecret,
        VerifyToken = VerifyToken,
        RejectInvalidSignatures = RejectInvalidSignatures,
        RateLimitPerMinute = RateLimitPerMinute,
        IpFilterMode = IpFilterMode,
        IpAllowlist = IpAllowlist,
        IpDenylist = IpDenylist,
        EncryptFields = EncryptFields,
        MaskFields = MaskFields,
        DedupEnabled = DedupEnabled,
        DedupStrategy = DedupStrategy,
        DedupWindowHours = DedupWindowHours,
        DedupCustomHeader = DedupCustomHeader,
        TransientMode = TransientMode,
        AllowedMethods = AllowedMethods
    };
}

/// <summary>
/// Serializes <see cref="UpdateSourceRequest"/> into the exact body <c>PATCH /api/sources/:id</c>
/// accepts, adding an explicit null for each field named in <see cref="UpdateSourceRequest.Clear"/>.
/// </summary>
public class UpdateSourceRequestConverter : JsonConverter<UpdateSourceRequest>
{
    /// <summary>The wire key each clearable field is nulled under.</summary>
    private static readonly Dictionary<SourceField, string> ClearableFields = new()
    {
        [SourceField.Description] = "description",
        [SourceField.Provider] = "provider",
        [SourceField.SigningSecret] = "signingSecret",
        [SourceField.VerifyToken] = "verifyToken",
        [SourceField.RateLimitPerMinute] = "rateLimitPerMinute",
        [SourceField.IpAllowlist] = "ipAllowlist",
        [SourceField.IpDenylist] = "ipDenylist",
        [SourceField.EncryptFields] = "encryptFields",
        [SourceField.MaskFields] = "maskFields",
        [SourceField.DedupStrategy] = "dedupStrategy",
        [SourceField.DedupWindowHours] = "dedupWindowHours",
        [SourceField.DedupCustomHeader] = "dedupCustomHeader",
        [SourceField.AllowedMethods] = "allowedMethods"
    };

    public override UpdateSourceRequest? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<SourceUpdatePayload>(ref reader, options)?.ToRequest();

    public override void Write(Utf8JsonWriter writer, UpdateSourceRequest value, JsonSerializerOptions options)
    {
        var payload = SourceUpdatePayload.From(value);

        if (value.Clear.Count == 0)
        {
            JsonSerializer.Serialize(writer, payload, options);
            return;
        }

        // As in UpdateEndpointRequestConverter: add the nulls to the serialized payload so the
        // payload type stays the single list of keys the API accepts.
        var body = JsonSerializer.SerializeToNode(payload, options)?.AsObject()
            ?? throw new InvalidOperationException("Serializing the source update body produced no object.");

        foreach (var field in value.Clear)
        {
            if (!ClearableFields.TryGetValue(field, out var key))
            {
                throw new InvalidOperationException(
                    $"UpdateSourceRequest.Clear names {field}, which the Hookbase API does not accept " +
                    "a null for. Clearable fields: " + string.Join(", ", ClearableFields.Keys) + ".");
            }

            if (body.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"UpdateSourceRequest.Clear names {field} but that field is also set on the same " +
                    "request; clear it or set it, not both.");
            }

            body[key] = null;
        }

        body.WriteTo(writer, options);
    }
}

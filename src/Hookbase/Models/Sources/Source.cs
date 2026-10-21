using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Sources;

/// <summary>
/// Webhook source - inbound webhook endpoint.
/// </summary>
public record Source
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public string? Provider { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsActive { get; init; } = true;

    // Signing secret fields - API returns either full secret OR masked info
    public string? SigningSecret { get; init; }

    [JsonConverter(typeof(NullableBooleanConverter))]
    public bool? HasSigningSecret { get; init; }

    public string? SigningSecretLast4 { get; init; }

    /// <summary>
    /// True when the provider verifies against a key Hookbase already holds (wise, wise-sandbox,
    /// aws-sns), so a missing signing secret is not a gap.
    /// </summary>
    [JsonConverter(typeof(BooleanConverter))]
    public bool VerifiesWithoutSecret { get; init; }

    /// <summary>
    /// True when the provider issues its secret only after the ingest URL is registered with it
    /// (asana, notion) and none has been set yet. Events are unverified until one is; for notion
    /// see <c>SourcesResource.GetPendingVerificationAsync</c>.
    /// </summary>
    [JsonConverter(typeof(BooleanConverter))]
    public bool AwaitingSigningSecret { get; init; }

    /// <summary>Number of events received. Returned by list and get.</summary>
    public int EventCount { get; init; }

    /// <summary>Number of routes from this source. Returned by list and get.</summary>
    public int RouteCount { get; init; }

    public string? IngestUrl { get; init; }

    [JsonPropertyName("rejectInvalidSignatures")]
    [JsonConverter(typeof(BooleanConverter))]
    public bool RejectInvalidSignatures { get; init; }

    // Deduplication fields
    [JsonConverter(typeof(BooleanConverter))]
    public bool DedupEnabled { get; init; }
    public string? DedupStrategy { get; init; }

    [JsonPropertyName("dedupWindowHours")]
    public int DedupWindowHours { get; init; } = 24;

    [JsonPropertyName("dedupCustomHeader")]
    public string? DedupCustomHeader { get; init; }

    // IP filtering
    public string? IpFilterMode { get; init; }
    public List<string> IpAllowlist { get; init; } = new();
    public List<string> IpDenylist { get; init; } = new();

    // Field masking/encryption
    public List<string> EncryptFields { get; init; } = new();
    public List<string> MaskFields { get; init; } = new();

    // Rate limiting
    [JsonPropertyName("rateLimitPerMinute")]
    public int? RateLimitPerMinute { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool TransientMode { get; init; }

    /// <summary>
    /// HTTP verbs this source's ingest endpoint accepts. An empty list means any method.
    /// </summary>
    public List<string> AllowedMethods { get; init; } = new();

    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Source as returned by create, carrying the credentials the API shows in full only once.
/// </summary>
public record SourceWithSecret : Source
{
    /// <summary>
    /// The full signing secret, or null when the provider has none to return: built-in key
    /// providers (wise, aws-sns), and asana or notion, whose secret arrives after registration
    /// (see <see cref="Source.AwaitingSigningSecret"/>). Read it again later with
    /// <c>SourcesResource.RevealSecretAsync</c>.
    /// </summary>
    public new string? SigningSecret { get; init; }

    /// <summary>
    /// The Meta verify token, for providers that use one (whatsapp); null otherwise. Read it again
    /// later with <c>SourcesResource.GetVerifyTokenAsync</c>.
    /// </summary>
    public string? VerifyToken { get; init; }
}

/// <summary>
/// Input for creating a new webhook source.
/// </summary>
public record CreateSourceRequest
{
    public required string Name { get; init; }

    /// <summary>
    /// Required: the slug forms the ingest URL, /ingest/&lt;org&gt;/&lt;slug&gt;, and cannot be
    /// changed after the source is created. POST /api/sources rejects a body without one.
    /// </summary>
    public required string Slug { get; init; }

    public string? Description { get; init; }
    public string? Provider { get; init; }

    /// <summary>
    /// The secret the provider signs with. What it must be depends on the provider (see
    /// <see cref="Hookbase.Models.Common.SecretSource"/>): vendor-issued secrets are required for
    /// some providers and format-checked, built-in key providers refuse one with a 400, and for
    /// operator-chosen secrets omitting it makes the API generate one, returned once on the
    /// created source.
    /// </summary>
    [JsonPropertyName("signingSecret")]
    public string? SigningSecret { get; init; }

    /// <summary>
    /// The Meta verify token (whatsapp only; any other provider is a 400). Omit it and the API
    /// generates one, returned once as <see cref="SourceWithSecret.VerifyToken"/>.
    /// </summary>
    [JsonPropertyName("verifyToken")]
    public string? VerifyToken { get; init; }

    [JsonPropertyName("rejectInvalidSignatures")]
    public bool? RejectInvalidSignatures { get; init; }

    public bool? DedupEnabled { get; init; }
    public string? DedupStrategy { get; init; }

    [JsonPropertyName("dedupWindowHours")]
    public int? DedupWindowHours { get; init; }

    [JsonPropertyName("dedupCustomHeader")]
    public string? DedupCustomHeader { get; init; }

    public string? IpFilterMode { get; init; }
    public List<string>? IpAllowlist { get; init; }
    public List<string>? IpDenylist { get; init; }
    public List<string>? EncryptFields { get; init; }
    public List<string>? MaskFields { get; init; }

    [JsonPropertyName("rateLimitPerMinute")]
    public int? RateLimitPerMinute { get; init; }

    public bool? TransientMode { get; init; }

    /// <summary>
    /// Restrict the ingest endpoint to these HTTP verbs. Null or empty accepts any method.
    /// </summary>
    public List<string>? AllowedMethods { get; init; }
}

/// <summary>
/// Input for updating an existing webhook source.
/// </summary>
/// <remarks>
/// A null member is omitted, so it leaves that setting alone. To send an explicit null (clear the
/// signing secret, regenerate the verify token, reset a list), name the field in
/// <see cref="Clear"/>. Serialization goes through <see cref="UpdateSourceRequestConverter"/>.
/// </remarks>
[JsonConverter(typeof(UpdateSourceRequestConverter))]
public record UpdateSourceRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Provider { get; init; }
    public bool? IsActive { get; init; }

    /// <summary>
    /// Replaces the signing secret, under the same per-provider rules as on create. Switching
    /// <see cref="Provider"/> to one with a vendor-issued secret needs the new secret in the same
    /// update (a 400 otherwise). To clear it, use <c>Clear = { SourceField.SigningSecret }</c>.
    /// </summary>
    [JsonPropertyName("signingSecret")]
    public string? SigningSecret { get; init; }

    /// <summary>
    /// Sets the Meta verify token (whatsapp only; any other provider is a 400). To have the API
    /// generate a fresh one, use <c>Clear = { SourceField.VerifyToken }</c>, or
    /// <c>SourcesResource.RotateVerifyTokenAsync</c>, which also returns it.
    /// </summary>
    [JsonPropertyName("verifyToken")]
    public string? VerifyToken { get; init; }

    [JsonPropertyName("rejectInvalidSignatures")]
    public bool? RejectInvalidSignatures { get; init; }

    public bool? DedupEnabled { get; init; }
    public string? DedupStrategy { get; init; }

    [JsonPropertyName("dedupWindowHours")]
    public int? DedupWindowHours { get; init; }

    [JsonPropertyName("dedupCustomHeader")]
    public string? DedupCustomHeader { get; init; }

    public string? IpFilterMode { get; init; }
    public List<string>? IpAllowlist { get; init; }
    public List<string>? IpDenylist { get; init; }
    public List<string>? EncryptFields { get; init; }
    public List<string>? MaskFields { get; init; }

    [JsonPropertyName("rateLimitPerMinute")]
    public int? RateLimitPerMinute { get; init; }

    public bool? TransientMode { get; init; }

    /// <summary>
    /// Restrict the ingest endpoint to these HTTP verbs. Pass an empty list to accept any method again.
    /// </summary>
    public List<string>? AllowedMethods { get; init; }

    /// <summary>
    /// Settings to send as an explicit JSON null.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>UpdateEndpointRequest.Clear</c>: null members are omitted, so null cannot also
    /// mean "set this back to nothing". Naming a field here sends a null for it:
    /// <code>
    /// await client.Sources.UpdateAsync("src_1", new UpdateSourceRequest
    /// {
    ///     Clear = { SourceField.SigningSecret }
    /// });
    /// </code>
    /// A field that is also set on this request throws rather than the SDK choosing one of the two.
    /// </remarks>
    [JsonIgnore]
    public List<SourceField> Clear { get; init; } = new();
}

/// <summary>
/// A source setting that <c>PATCH /api/sources/:id</c> accepts an explicit null for. Used by
/// <see cref="UpdateSourceRequest.Clear"/>.
/// </summary>
/// <remarks>
/// Exactly the <c>.nullable()</c> members of the API's <c>updateSourceSchema</c>; a null for any
/// other member is a 400, which is why this is an enum rather than a free string.
/// </remarks>
public enum SourceField
{
    /// <summary>Clears <c>description</c>.</summary>
    Description,

    /// <summary>Clears <c>provider</c>, which also clears the signing secret and turns off signature rejection.</summary>
    Provider,

    /// <summary>
    /// Clears <c>signingSecret</c>. For a provider whose secret Hookbase generates, use
    /// <c>RotateSecretAsync</c> to get a fresh one instead.
    /// </summary>
    SigningSecret,

    /// <summary>
    /// Sends <c>verifyToken: null</c>, which makes the API generate a fresh Meta verify token
    /// (whatsapp only). Same as <c>RotateVerifyTokenAsync</c> except the token is not returned.
    /// </summary>
    VerifyToken,

    /// <summary>Clears <c>rateLimitPerMinute</c> (no limit).</summary>
    RateLimitPerMinute,

    /// <summary>Clears <c>ipAllowlist</c>.</summary>
    IpAllowlist,

    /// <summary>Clears <c>ipDenylist</c>.</summary>
    IpDenylist,

    /// <summary>Clears <c>encryptFields</c>.</summary>
    EncryptFields,

    /// <summary>Clears <c>maskFields</c>.</summary>
    MaskFields,

    /// <summary>Clears <c>dedupStrategy</c>.</summary>
    DedupStrategy,

    /// <summary>Clears <c>dedupWindowHours</c>.</summary>
    DedupWindowHours,

    /// <summary>Clears <c>dedupCustomHeader</c>.</summary>
    DedupCustomHeader,

    /// <summary>Clears <c>allowedMethods</c> (any method).</summary>
    AllowedMethods
}

/// <summary>
/// A verification token a vendor delivered to a source's ingest URL during registration (notion),
/// parked until an operator adopts it as the signing secret.
/// </summary>
public record PendingVerification
{
    public string? Provider { get; init; }
    public string? Token { get; init; }
    public string? ReceivedAt { get; init; }
    public string? ClientIp { get; init; }
}

/// <summary>
/// Result of importing sources from a file.
/// </summary>
/// <remarks>
/// Same shape as a destination import. Applied: <c>Success</c>, <c>Summary</c> (<c>Imported</c>,
/// <c>Skipped</c>, <c>Overwritten</c>, <c>Failed</c>) and <c>Details</c>. With
/// <c>validateOnly</c>: <c>Valid</c>, <c>ValidationResults</c> and <c>Summary</c>
/// (<c>Total</c>, <c>ToCreate</c>, <c>ToOverwrite</c>, <c>ToSkip</c>, <c>Errors</c>).
/// </remarks>
public record ImportSourcesResult
{
    public bool Success { get; init; }

    /// <summary>Validate-only: whether no item has status <c>error</c>; null on an applied import.</summary>
    public bool? Valid { get; init; }

    public List<Hookbase.Models.Common.ImportValidationResult>? ValidationResults { get; init; }
    public Hookbase.Models.Destinations.ImportSummary? Summary { get; init; }
    public Hookbase.Models.Destinations.ImportDetails? Details { get; init; }

    /// <summary>Same as <c>Summary.Imported</c>.</summary>
    [JsonIgnore]
    [Obsolete("The API returns counts under summary. Use Summary.Imported.")]
    public int Imported => Summary?.Imported ?? 0;

    /// <summary>Same as <c>Summary.Skipped</c>.</summary>
    [JsonIgnore]
    [Obsolete("The API returns counts under summary. Use Summary.Skipped.")]
    public int Skipped => Summary?.Skipped ?? 0;

    /// <summary>The failed items from <c>Details.Failed</c>, as <c>"name: error"</c>.</summary>
    [JsonIgnore]
    [Obsolete("The API returns failures under details.failed. Use Details.Failed.")]
    public List<string>? Errors => Details?.Failed?
        .Select(f => $"{f.GetValueOrDefault("name")}: {f.GetValueOrDefault("error")}")
        .ToList();
}

/// <summary>
/// Export result containing sources with metadata.
/// </summary>
public record SourceExport
{
    public string? Version { get; init; }
    public string? ExportedAt { get; init; }
    public string? OrganizationSlug { get; init; }
    public List<Source>? Sources { get; init; }
}

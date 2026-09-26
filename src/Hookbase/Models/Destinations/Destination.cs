using Hookbase.Exceptions;
using Hookbase.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Destinations;

/// <summary>
/// Type of destination - HTTP endpoint or warehouse storage.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DestinationType
{
    Http,
    S3,
    R2,
    Gcs,
    AzureBlob
}

/// <summary>
/// S3 warehouse destination configuration.
/// </summary>
public record S3Config(
    string Bucket,
    string Region,
    string AccessKeyId,
    string SecretAccessKey,
    string? Prefix = null,
    string? FileFormat = null,
    string? PartitionBy = null
);

/// <summary>
/// Cloudflare R2 warehouse destination configuration.
/// </summary>
public record R2Config(
    string Bucket,
    string? Prefix = null,
    string? FileFormat = null,
    string? PartitionBy = null
);

/// <summary>
/// Google Cloud Storage warehouse destination configuration.
/// </summary>
public record GCSConfig(
    string Bucket,
    string ProjectId,
    string ServiceAccountKey,
    string? Prefix = null,
    string? FileFormat = null,
    string? PartitionBy = null
);

/// <summary>
/// Azure Blob Storage warehouse destination configuration.
/// </summary>
public record AzureBlobConfig(
    string AccountName,
    string AccountKey,
    string ContainerName,
    string? Prefix = null,
    string? FileFormat = null,
    string? PartitionBy = null
);

/// <summary>
/// Field mapping for warehouse destinations.
/// </summary>
public record FieldMapping(
    string Source,
    string Target,
    string Type,
    string? Default = null
);

/// <summary>
/// Throttling configuration for a destination. Replaces the legacy flat
/// <c>RateLimitPerMinute</c> field.
/// </summary>
/// <remarks>
/// <c>RateLimit</c> and <c>RateUnit</c> are required when <see cref="Mode"/> is
/// <c>"rate"</c>; <c>MaxConcurrency</c> is required when <see cref="Mode"/> is
/// <c>"concurrency"</c>. <c>QueueLimit</c> is optional in both modes.
/// </remarks>
public record Throttle
{
    /// <summary>One of "off", "rate", or "concurrency".</summary>
    public string Mode { get; init; } = "off";
    public int? RateLimit { get; init; }
    /// <summary>One of "second", "minute", or "hour".</summary>
    public string? RateUnit { get; init; }
    public int? MaxConcurrency { get; init; }
    public int? QueueLimit { get; init; }
}

/// <summary>
/// Webhook delivery destination.
/// </summary>
public record Destination
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public DestinationType? Type { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
    [JsonConverter(typeof(JsonStringStringDictionaryConverter))]
    public Dictionary<string, string>? Headers { get; init; }
    public string? AuthType { get; init; }
    [JsonConverter(typeof(JsonStringDictionaryConverter))]
    public Dictionary<string, object>? AuthConfig { get; init; }
    public int TimeoutMs { get; init; } = 30000;
    public Throttle? Throttle { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsActive { get; init; } = true;

    [JsonConverter(typeof(BooleanConverter))]
    public bool UseStaticIp { get; init; } = true;

    [JsonConverter(typeof(BooleanConverter))]
    public bool MockEnabled { get; init; }

    [JsonConverter(typeof(JsonStringDictionaryConverter))]
    public Dictionary<string, object>? MockConfig { get; init; }
    public JsonElement? Config { get; init; }
    public List<FieldMapping>? FieldMapping { get; init; }
    public int? BatchSize { get; init; }
    public int? BatchWindowSeconds { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }

    // List endpoint only - computed fields
    public int? RouteCount { get; init; }
    public int? SuccessCount { get; init; }
    public int? FailureCount { get; init; }
}

/// <summary>
/// Input for creating a new destination.
/// </summary>
public record CreateDestinationRequest
{
    private readonly string? _slug;

    public required string Name { get; init; }

    /// <summary>
    /// URL-safe identifier for the destination. The API requires it on create
    /// (<c>^[a-z0-9-]+$</c>, max 50 characters).
    /// </summary>
    /// <remarks>
    /// Optional here: when it is left unset or blank it is derived from <see cref="Name"/>, so a
    /// call that omits it still satisfies the API instead of 400ing. Reading this property returns
    /// the value that will be sent, or <c>null</c> when <see cref="Name"/> has nothing to derive
    /// from - in which case <c>CreateAsync</c> throws before any request is made. An explicitly
    /// supplied slug is never rewritten.
    /// </remarks>
    public string? Slug
    {
        get => string.IsNullOrWhiteSpace(_slug) ? TryDeriveSlug(Name) : _slug;
        init => _slug = value;
    }

    public DestinationType? Type { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? AuthType { get; init; }
    public Dictionary<string, object>? AuthConfig { get; init; }
    public int? TimeoutMs { get; init; }
    public Throttle? Throttle { get; init; }
    public object? Config { get; init; }
    public List<FieldMapping>? FieldMapping { get; init; }
    public bool? UseStaticIp { get; init; }
    public int? BatchSize { get; init; }
    public int? BatchWindowSeconds { get; init; }

    /// <summary>
    /// Returns this request with <see cref="Slug"/> resolved to the value that will be sent,
    /// deriving it from <see cref="Name"/> when the caller left it unset or blank.
    /// </summary>
    /// <remarks>
    /// Called by <c>DestinationsResource.CreateAsync</c> so an underivable name fails before any
    /// HTTP request instead of sending a body the API will reject.
    /// </remarks>
    /// <exception cref="HookbaseException">
    /// No slug was supplied and none can be derived from <see cref="Name"/>.
    /// </exception>
    public CreateDestinationRequest WithResolvedSlug()
        => string.IsNullOrWhiteSpace(_slug) ? this with { Slug = DeriveSlug(Name) } : this;

    /// <summary>
    /// Derives an API-acceptable slug (<c>^[a-z0-9-]+$</c>, max 50 characters) from a destination
    /// name.
    /// </summary>
    /// <remarks>
    /// Kept byte-identical to the other Hookbase SDKs (reference:
    /// <c>deriveDestinationSlug</c> in <c>node-sdk/src/resources/wire.ts</c>): decompose to NFKD,
    /// drop the non-spacing marks the decomposition leaves behind so <c>Café EU</c> yields
    /// <c>cafe-eu</c> rather than <c>caf-eu</c>, lowercase with the invariant culture (a
    /// culture-sensitive <c>ToLower</c> would turn <c>I</c> into <c>ı</c> under tr-TR), replace
    /// every run of non-alphanumerics with a single hyphen, trim hyphens, cut to 50 characters - a
    /// plain cut, which may land mid-word - then trim a hyphen the cut left behind. Characters that
    /// do not decompose (<c>Æ</c>, <c>Ø</c>) are dropped rather than folded; that is shared
    /// behaviour across the SDKs, not a bug to fix here alone.
    /// </remarks>
    /// <exception cref="HookbaseException">
    /// <paramref name="name"/> has no alphanumeric characters to derive a slug from.
    /// </exception>
    public static string DeriveSlug(string? name)
        => TryDeriveSlug(name) ?? throw new HookbaseException(
            $"Cannot derive a destination slug from name {JsonSerializer.Serialize(name)}: pass " +
            "Slug explicitly, as a string matching ^[a-z0-9-]+$ (max 50 characters).");

    /// <summary>Slug derivation that yields <c>null</c> instead of throwing. See <see cref="DeriveSlug"/>.</summary>
    private static string? TryDeriveSlug(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var decomposed = PreFold(name).Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);

        // Runes, not chars: a mark outside the BMP reaches a char loop as two surrogate halves,
        // neither of which reads as a mark, so it would survive the strip below and become a
        // separator while every other SDK dropped it.
        foreach (var rune in decomposed.EnumerateRunes())
        {
            // Drop the combining marks the decomposition left behind, so the base letter survives
            // as a letter instead of becoming a separator.
            if (IsSlugMark(rune))
            {
                continue;
            }

            var lowered = Rune.ToLowerInvariant(rune);
            if (lowered.Value is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append((char)lowered.Value);
            }
            else if (builder.Length > 0 && builder[builder.Length - 1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().TrimEnd('-');
        if (slug.Length > SlugMaxLength)
        {
            slug = slug.Substring(0, SlugMaxLength).TrimEnd('-');
        }

        return slug.Length == 0 ? null : slug;
    }

    /// <summary>
    /// Whether <paramref name="rune"/> is a combining mark the slug fold drops.
    /// </summary>
    /// <remarks>
    /// Which characters are non-spacing marks is a function of the Unicode version the *runtime*
    /// ships, and the four SDKs' runtimes do not agree: .NET 8's tables are Unicode 15.1 while the
    /// reference implementation's <c>\p{Mn}</c> (V8) is a version ahead. Reading the category alone
    /// therefore makes the same name slug differently depending on which SDK created the
    /// destination, which is the thing this derivation exists to prevent. <see cref="SlugMarkAdditions"/>
    /// and <see cref="SlugMarkReclassified"/> close that gap, and both self-heal: once the runtime's
    /// own tables agree, they are redundant but harmless.
    /// </remarks>
    private static bool IsSlugMark(Rune rune)
    {
        // Mn in Unicode 15.0 and Mc - a *spacing* mark - from 15.1, so the reference does not strip
        // it and neither may this SDK: it has to separate, the way any other non-alphanumeric does.
        if (rune.Value == SlugMarkReclassified)
        {
            return false;
        }

        if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.NonSpacingMark)
        {
            return true;
        }

        foreach (var (low, high) in SlugMarkAdditions)
        {
            if (rune.Value >= low && rune.Value <= high)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Characters the reference runtime folds to ASCII that this runtime's Unicode tables predate,
    /// applied before normalizing so NFKD's answer is the same on both. Every mapping is the
    /// reference's own NFKD output; a runtime that knows these characters decomposes them
    /// identically, so this stays a no-op rather than a divergence of its own.
    /// </summary>
    /// <remarks>
    /// Rebuilding the string unconditionally also sanitizes it: <see cref="string.Normalize(NormalizationForm)"/>
    /// throws <see cref="ArgumentException"/> on an unpaired surrogate, where the reference runtime
    /// passes one through to become a separator. <see cref="StringRuneEnumerator"/> yields U+FFFD for
    /// an unpaired surrogate, which separates for the same reason, so a malformed name derives a slug
    /// here instead of throwing an exception this SDK does not document.
    /// </remarks>
    private static string PreFold(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var rune in name.EnumerateRunes())
        {
            // U+A7F1 (Latin Extended-D) decomposes to "S"; U+1CCD6-U+1CCF9 are 36 contiguous
            // additions in Symbols for Legacy Computing Supplement decomposing to A-Z then 0-9.
            if (rune.Value == 0xA7F1)
            {
                builder.Append('S');
            }
            else if (rune.Value is >= 0x1CCD6 and <= 0x1CCF9)
            {
                builder.Append(SlugPreFoldAlphabet[rune.Value - 0x1CCD6]);
            }
            else if (IsNormalizeHostile(rune))
            {
                builder.Append(Rune.ReplacementChar);
            }
            else
            {
                builder.Append(rune);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Whether <see cref="string.Normalize(NormalizationForm)"/> refuses this character: the 66
    /// Unicode noncharacters. The reference runtime normalizes them without complaint and they end
    /// up separating, like any other non-alphanumeric, so <see cref="PreFold"/> swaps in U+FFFD -
    /// which also separates - rather than letting a name containing one throw
    /// <see cref="ArgumentException"/> out of a slug derivation. Reading
    /// <see cref="CreateDestinationRequest.Slug"/> derives, so that exception could surface from a
    /// property getter.
    /// </summary>
    private static bool IsNormalizeHostile(Rune rune)
        => (rune.Value & 0xFFFE) == 0xFFFE || rune.Value is >= 0xFDD0 and <= 0xFDEF;

    /// <summary>
    /// 75 code points in 21 ranges: marks the reference runtime strips that Unicode 15.0 does not
    /// know. The same table <c>slugMarkAdditions</c> carries in <c>go-sdk/slug_fold.go</c> and
    /// <c>_SLUG_MARK_ADDITIONS</c> in <c>python-sdk/src/hookbase/models/_wire.py</c>. Change one and
    /// you change all three.
    /// </summary>
    private static readonly (int Low, int High)[] SlugMarkAdditions =
    [
        (0x0897, 0x0897),
        (0x1ACF, 0x1ADD),
        (0x1AE0, 0x1AEB),
        (0x10D69, 0x10D6D),
        (0x10EFA, 0x10EFC),
        (0x113BB, 0x113C0),
        (0x113CE, 0x113CE),
        (0x113D0, 0x113D0),
        (0x113D2, 0x113D2),
        (0x113E1, 0x113E2),
        (0x11B60, 0x11B60),
        (0x11B62, 0x11B64),
        (0x11B66, 0x11B66),
        (0x11F5A, 0x11F5A),
        (0x1611E, 0x16129),
        (0x1612D, 0x1612F),
        (0x1E5EE, 0x1E5EF),
        (0x1E6E3, 0x1E6E3),
        (0x1E6E6, 0x1E6E6),
        (0x1E6EE, 0x1E6EF),
        (0x1E6F5, 0x1E6F5),
    ];

    /// <summary>AHOM CONSONANT SIGN MEDIAL RA: Mn in Unicode 15.0, Mc since 15.1.</summary>
    private const int SlugMarkReclassified = 0x1171E;

    /// <summary>What <see cref="PreFold"/>'s second range decomposes to, in order.</summary>
    private const string SlugPreFoldAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>Max slug length, as <c>createDestinationSchema</c> enforces it.</summary>
    private const int SlugMaxLength = 50;
}

/// <summary>
/// Input for updating an existing destination.
/// </summary>
public record UpdateDestinationRequest
{
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? AuthType { get; init; }
    public Dictionary<string, object>? AuthConfig { get; init; }
    public int? TimeoutMs { get; init; }
    public Throttle? Throttle { get; init; }
    public bool? IsActive { get; init; }
    public object? Config { get; init; }
    public List<FieldMapping>? FieldMapping { get; init; }
    public bool? UseStaticIp { get; init; }
    public int? BatchSize { get; init; }
    public int? BatchWindowSeconds { get; init; }
}

/// <summary>
/// Result of testing a destination connection.
/// </summary>
public record TestDestinationResult
{
    public bool Success { get; init; }
    public int? Status { get; init; }
    public int? LatencyMs { get; init; }
    public string? ResponseBody { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Export result containing destinations with metadata.
/// </summary>
public record DestinationExport
{
    public string? Version { get; init; }
    public string? ExportedAt { get; init; }
    public string? OrganizationSlug { get; init; }
    public List<Destination>? Destinations { get; init; }
}

/// <summary>
/// Request for importing destinations.
/// </summary>
public record ImportDestinationsRequest
{
    public required List<CreateDestinationRequest> Destinations { get; init; }
    public string ConflictStrategy { get; init; } = "skip"; // skip, rename, overwrite
    public bool? ValidateOnly { get; init; }
}

/// <summary>
/// Result of importing destinations.
/// </summary>
public record ImportDestinationsResult
{
    public bool Success { get; init; }
    public ImportSummary? Summary { get; init; }
    public ImportDetails? Details { get; init; }
}

public record ImportSummary
{
    public int Imported { get; init; }
    public int Skipped { get; init; }
    public int Overwritten { get; init; }
    public int Failed { get; init; }
}

public record ImportDetails
{
    public List<string>? Imported { get; init; }
    public List<string>? Skipped { get; init; }
    public List<string>? Overwritten { get; init; }
    public List<Dictionary<string, string>>? Failed { get; init; }
}

/// <summary>
/// Request for bulk delete operations.
/// </summary>
public record BulkDeleteRequest
{
    public required List<string> Ids { get; init; }
}

/// <summary>
/// Result of bulk delete operation.
/// </summary>
public record BulkDeleteResult
{
    public bool Success { get; init; }
    public int Deleted { get; init; }
}

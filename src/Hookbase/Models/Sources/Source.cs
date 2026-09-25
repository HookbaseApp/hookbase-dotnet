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
/// Source with full signing secret (returned on creation/rotation).
/// </summary>
public record SourceWithSecret : Source
{
    public new string? SigningSecret { get; init; }
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
    /// Supply your own secret to match what the provider is already configured with. Omit it and
    /// the API generates one, returned once on the created source.
    /// </summary>
    [JsonPropertyName("signingSecret")]
    public string? SigningSecret { get; init; }

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
public record UpdateSourceRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Provider { get; init; }
    public bool? IsActive { get; init; }

    [JsonPropertyName("signingSecret")]
    public string? SigningSecret { get; init; }

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
}

/// <summary>
/// Result of importing sources from a file.
/// </summary>
public record ImportSourcesResult
{
    public int Imported { get; init; }
    public int Skipped { get; init; }
    public List<string>? Errors { get; init; }
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

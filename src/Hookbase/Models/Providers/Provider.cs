using System.Text.Json;

namespace Hookbase.Models.Providers;

/// <summary>
/// The part of a provider's signature verification the catalog list carries: enough to build a
/// create-source flow without fetching every provider.
/// </summary>
/// <remarks>
/// The list response gained this block after the detail route had it, so an API that has not
/// shipped it yet leaves <see cref="ProviderSummary.SignatureVerification"/> null on every entry;
/// fall back to <c>ProvidersResource.GetAsync</c> for <see cref="ProviderSignatureVerification"/>.
/// </remarks>
public record ProviderSignatureSummary
{
    public bool Supported { get; init; }

    /// <summary><c>hmac</c>, <c>shared_secret</c>, <c>bespoke</c> or <c>asymmetric</c>; null when the entry has no scheme.</summary>
    public string? Kind { get; init; }

    /// <summary>One of <see cref="Hookbase.Models.Common.SecretSource"/>.</summary>
    public string? SecretSource { get; init; }

    /// <summary>The vendor's own name for the credential ("Signing secret", "Public key", ...).</summary>
    public string? SecretLabel { get; init; }

    public string? SecretPlaceholder { get; init; }

    /// <summary>
    /// True when the vendor issues the secret only once the ingest URL is registered with it
    /// (asana, notion), so a source is created without one.
    /// </summary>
    public bool SecretAfterRegistration { get; init; }
}

/// <summary>
/// A second, Hookbase-generated credential the vendor checks at registration (Meta's "Verify token").
/// </summary>
public record ProviderVerifyTokenInfo
{
    public string? Label { get; init; }
    public string? Instruction { get; init; }
}

/// <summary>
/// A provider's full signature verification block, from <c>ProvidersResource.GetAsync</c>.
/// </summary>
public record ProviderSignatureVerification : ProviderSignatureSummary
{
    public string? Algorithm { get; init; }
    public string? HeaderName { get; init; }
    public string? SecretLocationHint { get; init; }

    /// <summary>Where the value comes from or goes, in the vendor's own UI wording.</summary>
    public string? SecretInstruction { get; init; }

    /// <summary><c>whsec</c> or <c>basic_auth</c> when the API enforces a shape on the secret.</summary>
    public string? SecretFormat { get; init; }

    /// <summary>Set for providers that also need a verify token (whatsapp).</summary>
    public ProviderVerifyTokenInfo? VerifyToken { get; init; }
}

/// <summary>
/// One event type a provider sends.
/// </summary>
public record ProviderEventType
{
    public string? Type { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// A provider as the catalog list returns it.
/// </summary>
/// <remarks>
/// <see cref="Id"/> is the catalog's own id, which can differ from the value a source takes: use
/// <see cref="HookbaseProvider"/> as <c>CreateSourceRequest.Provider</c> (catalog
/// <c>mollie-nextgen</c> is provider <c>mollie</c>, and several entries map to <c>custom</c>).
/// </remarks>
public record ProviderSummary
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public int EventTypeCount { get; init; }
    public string? HookbaseProvider { get; init; }
    public bool HasSignatureVerification { get; init; }
    public bool HasIpRanges { get; init; }
    public ProviderSignatureSummary? SignatureVerification { get; init; }
}

/// <summary>
/// The catalog list response.
/// </summary>
public record ProviderList
{
    public List<ProviderSummary> Providers { get; init; } = new();

    /// <summary>Every catalog category, not only those in this (possibly filtered) result.</summary>
    public List<string> Categories { get; init; } = new();

    public int Total { get; init; }
}

/// <summary>
/// A provider's full catalog entry. See <see cref="ProviderSummary"/> on <c>Id</c> versus
/// <c>HookbaseProvider</c>.
/// </summary>
public record Provider
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public string? Website { get; init; }
    public string? DocsUrl { get; init; }
    public string? Icon { get; init; }
    public string? HookbaseProvider { get; init; }

    /// <summary>Null for providers that do not sign their webhooks.</summary>
    public ProviderSignatureVerification? SignatureVerification { get; init; }

    public List<ProviderEventType> EventTypes { get; init; } = new();

    /// <summary>Example bodies keyed by event type.</summary>
    public Dictionary<string, JsonElement> SamplePayloads { get; init; } = new();

    /// <summary>Null when the vendor publishes no IP ranges.</summary>
    public List<string>? IpRanges { get; init; }

    public List<string> SetupGuide { get; init; } = new();
}

/// <summary>
/// The event types and sample payloads for one provider.
/// </summary>
public record ProviderEvents
{
    public string? ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public List<ProviderEventType> EventTypes { get; init; } = new();
    public Dictionary<string, JsonElement> SamplePayloads { get; init; } = new();
}

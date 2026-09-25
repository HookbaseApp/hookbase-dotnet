using System.Text.Json.Serialization;

namespace Hookbase.Models.Common;

/// <summary>
/// Providers the API will accept for a source.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors <c>SUPPORTED_SIGNATURE_PROVIDERS</c> in the API
/// (<c>api/src/utils/signature-schemes.ts</c>), which is derived from the signature scheme
/// table rather than restated. Regenerate with <c>npx tsx scripts/print-source-enums.ts</c>
/// in the api package. Do not hand-add members: a value the API does not accept is a 400 the
/// type promised would not happen.
/// </para>
/// <para>
/// These are string constants rather than an enum because <c>CreateSourceRequest.Provider</c>
/// is a <c>string?</c> — every model in this SDK carries these fields as strings — so the
/// previous enum could not be assigned to the property it named. (The example in
/// <c>examples/BasicUsage</c> tried to, and did not compile; the examples are not in
/// <c>Hookbase.sln</c>, so CI never caught it.) Constants also pin the exact wire value
/// instead of leaving it to whichever <c>JsonNamingPolicy</c> happens to be in effect.
/// </para>
/// <para>
/// The previous list was wrong in both directions. <c>Pipedrive</c>, <c>Hubspot</c>,
/// <c>Salesforce</c>, <c>Webflow</c>, <c>Clickfunnels</c> and <c>Webhook</c> were never
/// providers the API accepted and do not exist anywhere else in this product;
/// <c>Sendgrid</c> and <c>Mailgun</c> were never accepted either (SendGrid signs with ECDSA,
/// Mailgun puts its signature in the POST body, so neither fits this scheme model). It was
/// also missing <c>Custom</c>, the fallback every unsupported provider is meant to use.
/// </para>
/// </remarks>
public static class SourceProvider
{
    public const string Airtable = "airtable";
    public const string Asana = "asana";
    public const string Bitbucket = "bitbucket";
    public const string Calendly = "calendly";
    public const string Custom = "custom";
    public const string Generic = "generic";
    public const string GitHub = "github";
    public const string GitLab = "gitlab";
    public const string Heroku = "heroku";
    public const string Intercom = "intercom";
    public const string LemonSqueezy = "lemonsqueezy";
    public const string Notion = "notion";
    public const string Paddle = "paddle";
    public const string Razorpay = "razorpay";
    public const string Sentry = "sentry";
    public const string Shopify = "shopify";
    public const string Slack = "slack";
    public const string StandardWebhooks = "standard-webhooks";
    public const string Stripe = "stripe";

    /// <summary>Alias of <see cref="StandardWebhooks"/>; both resolve to the same scheme.</summary>
    public const string Svix = "svix";

    public const string Twilio = "twilio";
    public const string Typeform = "typeform";
    public const string WorkOS = "workos";
    public const string Zoom = "zoom";
}

/// <summary>
/// Deduplication strategies the API will accept. <see cref="Auto"/> is the default.
/// </summary>
/// <remarks>
/// Corrected alongside <see cref="SourceProvider"/>: <c>Header</c>, <c>Payload</c> and
/// <c>Both</c> were never accepted, and <c>provider_id</c>, <c>payload_hash</c> and
/// <c>idempotency_key</c> were missing. Source of truth is <c>VALID_DEDUP_STRATEGIES</c> in
/// <c>api/src/routes/sources.ts</c>.
/// </remarks>
public static class DedupStrategy
{
    public const string Auto = "auto";
    public const string ProviderId = "provider_id";
    public const string PayloadHash = "payload_hash";
    public const string IdempotencyKey = "idempotency_key";
    public const string None = "none";
}

/// <summary>
/// IP filter modes the API will accept.
/// </summary>
/// <remarks>
/// <c>Allow</c> and <c>Deny</c> were the wrong spellings; the API accepts <c>allowlist</c> and
/// <c>denylist</c>.
/// </remarks>
public static class IpFilterMode
{
    public const string None = "none";
    public const string Allowlist = "allowlist";
    public const string Denylist = "denylist";
    public const string Both = "both";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HttpMethodType
{
    Get,
    Post,
    Put,
    Patch,
    Delete
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthType
{
    None,
    Bearer,
    Basic,
    ApiKey,
    Oauth2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CircuitStatus
{
    Closed,
    Open,
    HalfOpen
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InboundEventStatus
{
    Received,
    Processing,
    Delivered,
    PartiallyDelivered,
    Failed,
    Filtered
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeliveryStatus
{
    Pending,
    Queued,
    Sending,
    Delivered,
    Failed,
    Exhausted
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageStatus
{
    Pending,
    Success,
    Failed,
    Exhausted
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FilterOperator
{
    Equals,
    NotEquals,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    In,
    NotIn,
    Exists,
    NotExists,
    Regex
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortOrder
{
    Asc,
    Desc
}

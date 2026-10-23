using System.Text.Json.Serialization;

namespace Hookbase.Models.Common;

/// <summary>
/// Providers the API accepted for a source when this version was released.
/// </summary>
/// <remarks>
/// <para>
/// <c>Provider</c> is an open string on every model, and these are suggestions, not a closed
/// set: the API validates the value against <c>SUPPORTED_SIGNATURE_PROVIDERS</c>
/// (<c>api/src/utils/signature-schemes.ts</c>) and answers anything else with a 400 that says
/// so, so a provider added server-side later can be passed as a literal string without waiting
/// for an SDK release. Regenerate this list with <c>npx tsx scripts/print-source-enums.ts</c> in
/// the api package rather than hand-adding members.
/// </para>
/// <para>
/// These are string constants rather than an enum because <c>CreateSourceRequest.Provider</c>
/// is a <c>string?</c>, and constants pin the exact wire value instead of leaving it to whichever
/// <c>JsonNamingPolicy</c> happens to be in effect.
/// </para>
/// <para>
/// <c>Sendgrid</c>, <c>Mailgun</c> and <c>Linear</c> were removed in an earlier correction because the API did not
/// accept them then; it does now, with real verification schemes, so they are back.
/// <c>Svix</c>, <c>Clerk</c>, <c>Resend</c>, <c>Render</c>, <c>Etsy</c> and <c>Stytch</c> are
/// aliases that verify as <see cref="StandardWebhooks"/>, and <see cref="Generic"/> as
/// <see cref="Custom"/>.
/// </para>
/// <para>
/// What a source's signing secret must be depends on the provider; read it from the provider
/// catalog (<c>client.Providers.GetAsync(id)</c>, <c>SignatureVerification.SecretSource</c>) and
/// see <see cref="SecretSource"/>.
/// </para>
/// </remarks>
public static class SourceProvider
{
    public const string Adyen = "adyen";
    public const string Airtable = "airtable";
    public const string Asana = "asana";
    public const string Auth0 = "auth0";
    public const string AwsSns = "aws-sns";
    public const string Bigcommerce = "bigcommerce";
    public const string Bitbucket = "bitbucket";
    public const string Braintree = "braintree";
    public const string Calendly = "calendly";
    public const string Chargebee = "chargebee";
    public const string Circleci = "circleci";
    public const string Clerk = "clerk";
    public const string Clickup = "clickup";
    public const string Cloudflare = "cloudflare";
    public const string CoinbaseCommerce = "coinbase-commerce";
    public const string Custom = "custom";
    public const string Datadog = "datadog";
    public const string Discord = "discord";
    public const string Docusign = "docusign";
    public const string Etsy = "etsy";
    public const string Freshdesk = "freshdesk";
    public const string Generic = "generic";
    public const string GitHub = "github";
    public const string GitLab = "gitlab";
    public const string GitLabSigningToken = "gitlab-signing-token";
    public const string Gocardless = "gocardless";
    public const string Grafana = "grafana";
    public const string GrafanaHmac = "grafana-hmac";
    public const string GrafanaHmacTimestamped = "grafana-hmac-timestamped";
    public const string Heroku = "heroku";
    public const string Hubspot = "hubspot";
    public const string Intercom = "intercom";
    public const string Jira = "jira";
    public const string Klaviyo = "klaviyo";
    public const string Launchdarkly = "launchdarkly";
    public const string LemonSqueezy = "lemonsqueezy";
    public const string Linear = "linear";
    public const string Mailchimp = "mailchimp";
    public const string Mailgun = "mailgun";
    public const string Mollie = "mollie";
    public const string Netlify = "netlify";
    public const string Newrelic = "newrelic";
    public const string Notion = "notion";
    public const string Okta = "okta";
    public const string Paddle = "paddle";
    public const string Pagerduty = "pagerduty";
    public const string Paypal = "paypal";
    public const string Pipedrive = "pipedrive";
    public const string Postmark = "postmark";
    public const string Railway = "railway";
    public const string Razorpay = "razorpay";
    public const string Render = "render";
    public const string Resend = "resend";
    public const string Segment = "segment";
    public const string Sendgrid = "sendgrid";
    public const string Sentry = "sentry";
    public const string Shopify = "shopify";
    public const string Slack = "slack";
    public const string Square = "square";
    public const string StandardWebhooks = "standard-webhooks";
    public const string Stripe = "stripe";
    public const string Stytch = "stytch";
    public const string Supabase = "supabase";
    public const string SupabaseAuthHooks = "supabase-auth-hooks";

    /// <summary>Alias of <see cref="StandardWebhooks"/>; both resolve to the same scheme.</summary>
    public const string Svix = "svix";

    public const string Telegram = "telegram";
    public const string TerraformCloud = "terraform-cloud";
    public const string Trello = "trello";
    public const string Twilio = "twilio";
    public const string Typeform = "typeform";
    public const string Vercel = "vercel";
    public const string Whatsapp = "whatsapp";
    public const string Wise = "wise";
    public const string WiseSandbox = "wise-sandbox";
    public const string Woocommerce = "woocommerce";
    public const string WorkOS = "workos";
    public const string Zendesk = "zendesk";
    public const string Zoom = "zoom";
}


/// <summary>
/// Which side produces a provider's signing credential: the catalog's
/// <c>signatureVerification.secretSource</c>.
/// </summary>
public static class SecretSource
{
    /// <summary>
    /// The vendor issues the secret; paste it as <c>SigningSecret</c>. For several providers
    /// (standard-webhooks, svix, clerk, resend, airtable, gitlab-signing-token,
    /// supabase-auth-hooks, jira, and the discord and sendgrid public keys) it is required on
    /// create and format-checked, both as a 400.
    /// </summary>
    public const string Provider = "provider";

    /// <summary>
    /// The vendor never issues one. Omit <c>SigningSecret</c> and Hookbase generates it, returned
    /// once on the created source; paste that into the vendor.
    /// </summary>
    public const string Operator = "operator";

    /// <summary>Paste the vendor's public verification key (PEM or base64).</summary>
    public const string PublicKey = "public_key";

    /// <summary>
    /// Hookbase already holds the vendor's key (wise, wise-sandbox, aws-sns). Send no secret:
    /// one is a 400. The source reports <c>VerifiesWithoutSecret</c>.
    /// </summary>
    public const string BuiltIn = "built_in";

    /// <summary>The vendor does not sign; a secret may still be set for custom HMAC.</summary>
    public const string None = "none";
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

/// <summary>
/// Inbound event status, used as the <c>status</c> filter on event lists. Sent as the API's
/// snake_case names: <c>delivered</c>, <c>failed</c>, <c>pending</c>, <c>partial</c>,
/// <c>duplicate</c>, <c>no_routes</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InboundEventStatus
{
    [Obsolete("Not an API event status; filtering by it matches nothing. Use Pending.")]
    Received,
    [Obsolete("Not an API event status; filtering by it matches nothing. Use Pending.")]
    Processing,
    Delivered,
    [Obsolete("Not an API event status; filtering by it matches nothing. Use Partial.")]
    PartiallyDelivered,
    Failed,
    [Obsolete("Not an API event status; filtering by it matches nothing. Use NoRoutes.")]
    Filtered,
    Pending,
    Partial,
    Duplicate,
    NoRoutes
}

/// <summary>
/// Inbound delivery status, used as the <c>status</c> filter on delivery lists. Sent as the API's
/// snake_case names: <c>pending</c>, <c>delivered</c>, <c>failed</c>, <c>failed_over</c>,
/// <c>schema_failed</c>, <c>throttled</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeliveryStatus
{
    Pending,
    [Obsolete("Not an API delivery status; filtering by it matches nothing. Use Pending.")]
    Queued,
    [Obsolete("Not an API delivery status; filtering by it matches nothing. Use Pending.")]
    Sending,
    Delivered,
    Failed,
    [Obsolete("Not an API delivery status; filtering by it matches nothing. Use Failed.")]
    Exhausted,
    FailedOver,
    SchemaFailed,
    Throttled
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

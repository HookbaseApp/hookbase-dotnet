using System.Text.Json.Serialization;

namespace Hookbase.Models.Common;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SourceProvider
{
    Generic,
    Github,
    Stripe,
    Shopify,
    Slack,
    Twilio,
    Sendgrid,
    Mailgun,
    Paddle,
    Lemonsqueezy,
    Pipedrive,
    Hubspot,
    Salesforce,
    Webflow,
    Clickfunnels,
    Webhook
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DedupStrategy
{
    None,
    Auto,
    Header,
    Payload,
    Both
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IpFilterMode
{
    None,
    Allow,
    Deny,
    Both
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

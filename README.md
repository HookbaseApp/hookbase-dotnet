# Hookbase .NET SDK

Official .NET SDK for [Hookbase](https://hookbase.app) - webhook relay platform for receiving, transforming, and routing webhooks to multiple destinations.

[![NuGet](https://img.shields.io/nuget/v/Hookbase.svg)](https://www.nuget.org/packages/Hookbase)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

- **Async/await first** - All methods return `Task<T>` for async operations
- **Type-safe** - Full TypeScript-like type definitions using C# records
- **Automatic pagination** - Use `ListAllAsync()` to iterate through all resources
- **Dual pagination support** - Offset-based (inbound) and cursor-based (outbound)
- **Comprehensive error handling** - Typed exceptions for different HTTP status codes
- **Webhook verification** - Built-in Standard Webhooks verification: HMAC-SHA256 (`v1`) and Ed25519 (`v1a`)
- **Retry logic** - Exponential backoff with jitter for failed requests
- **Broad API coverage** - Inbound and outbound resources; see [API Coverage](#api-coverage)

## Installation

```bash
dotnet add package Hookbase
```

## Quick Start

### Initialize the client

```csharp
using Hookbase;

var client = new HookbaseClient("whr_your_api_key");
```

Or with custom options:

```csharp
var client = new HookbaseClient(new HookbaseClientOptions
{
    ApiKey = "whr_your_api_key",
    BaseUrl = "https://api.hookbase.app",
    Timeout = 60,
    MaxRetries = 5
});
```

### Working with Sources (Inbound Webhooks)

```csharp
// List sources with pagination
var sources = await client.Sources.ListAsync(page: 1, pageSize: 20);
Console.WriteLine($"Total sources: {sources.Total}");

foreach (var s in sources.Data)
{
    Console.WriteLine($"{s.Name} ({s.Slug})");
}

// Create a new source
var newSource = await client.Sources.CreateAsync(new CreateSourceRequest
{
    Name = "GitHub Webhooks",
    Slug = "github-prod",
    Provider = SourceProvider.GitHub,
    RejectInvalidSignatures = true
});

Console.WriteLine($"Created source: {newSource.Id}");
// Null for providers with no secret to return (wise, asana, notion); see below.
Console.WriteLine($"Signing secret: {newSource.SigningSecret}");

// Get a source
var source = await client.Sources.GetAsync("source_123");

// Update a source. UpdateAsync returns no body; re-fetch if you need the result.
await client.Sources.UpdateAsync("source_123", new UpdateSourceRequest
{
    Name = "GitHub Production",
    IsActive = true
});

// Rotate signing secret
var rotatedSecret = await client.Sources.RotateSecretAsync("source_123");
Console.WriteLine($"New secret: {rotatedSecret}");

// Clear a field: null members are omitted, so name it in Clear to send an explicit null
await client.Sources.UpdateAsync("source_123", new UpdateSourceRequest
{
    Clear = { SourceField.SigningSecret }
});

// Delete a source
await client.Sources.DeleteAsync("source_123");
```

What goes in `SigningSecret` depends on the provider. `Provider` is an open string
(`SourceProvider` lists the values known at release); the provider catalog says which kind of
secret each one takes:

```csharp
var provider = await client.Providers.GetAsync("stripe");
switch (provider.SignatureVerification?.SecretSource)
{
    case SecretSource.Provider:
    case SecretSource.PublicKey:
        // paste the vendor's value into SigningSecret
        break;
    case SecretSource.Operator:
        // omit it; CreateAsync returns the generated secret once
        break;
    case SecretSource.BuiltIn:
        // send none; Source.VerifiesWithoutSecret is true
        break;
}
```

WhatsApp sources also carry a Meta verify token (`CreateSourceRequest.VerifyToken`,
`GetVerifyTokenAsync`, `RotateVerifyTokenAsync`). Notion delivers its secret to the ingest URL
after registration: read it with `GetPendingVerificationAsync`, paste it into Notion's verify
form, then `AdoptPendingVerificationAsync`.

### Auto-pagination with IAsyncEnumerable

```csharp
// Iterate through ALL sources automatically
await foreach (var source in client.Sources.ListAllAsync())
{
    Console.WriteLine($"Processing: {source.Name}");
    // SDK automatically handles pagination
}
```

The `search` and `isActive` parameters of `Sources.ListAsync` and `ListAllAsync` are sent, but
the API does not filter on them yet, so filter the results yourself.

### Working with Applications (Outbound Webhooks)

```csharp
// List applications (cursor-based pagination)
var apps = await client.Applications.ListAsync(limit: 50);

foreach (var a in apps.Data)
{
    Console.WriteLine($"{a.Name} - {a.ExternalId}");
}

if (apps.HasMore)
{
    var nextPage = await client.Applications.ListAsync(cursor: apps.NextCursor);
}

// Create application
var app = await client.Applications.CreateAsync(new CreateApplicationRequest
{
    Name = "Customer Portal",
    ExternalId = "customer_12345",
    Metadata = new Dictionary<string, object>
    {
        ["plan"] = "pro",
        ["region"] = "us-east"
    }
});

// Get or create (idempotent)
var appOrCreate = await client.Applications.GetOrCreateAsync(new GetOrCreateApplicationRequest
{
    ExternalId = "customer_12345",
    Name = "Customer Portal",
    Metadata = new Dictionary<string, object> { ["created"] = "2024" }
});
```

### Sending Webhook Messages

```csharp
// Send a message to all subscribed endpoints in the API key's organization
var response = await client.Messages.SendAsync(new SendMessageRequest
{
    EventType = "payment.succeeded",
    Payload = new Dictionary<string, object>
    {
        ["amount"] = 4999,
        ["currency"] = "usd",
        ["customer_id"] = "cust_123"
    },
    IdempotencyKey = "payment_123_succeeded" // Optional; one is generated when unset
});

Console.WriteLine($"Event ID: {response.EventId}");
Console.WriteLine($"Queued for {response.MessagesQueued} endpoints");

// Send to specific endpoints, or to one application's endpoints, only
var targeted = await client.Messages.SendAsync(new SendMessageRequest
{
    EventType = "order.created",
    Payload = new Dictionary<string, object> { ["order_id"] = "ord_456" },
    EndpointIds = new List<string> { "ep_123", "ep_456" }
});

// Delivery attempts of one message, and the dead-letter queue
var attempts = await client.Messages.ListAttemptsAsync("msg_123");
var dead = await client.Dlq.ListAsync(new DlqListOptions { EndpointId = "ep_123" });
var retried = await client.Dlq.RetryBulkAsync(dead.Data.Select(m => m.Id!));
```

Every `Messages` method except `ListAttemptsAsync` also has an overload that takes an organization
ID first (`SendAsync("org_123", request)`), which calls the organization-scoped routes. Before
5.1.0 those were the only overloads.

### Managing Endpoints

```csharp
// Create endpoint
var endpoint = await client.Endpoints.CreateAsync(new CreateEndpointRequest
{
    ApplicationId = "app_123",
    Url = "https://customer.example.com/webhooks",
    Description = "Production webhook endpoint",
    Headers = new() { ("X-Api-Key", "customer-secret") },
    TimeoutSeconds = 30,
    RateLimitPerSecond = 100,
    SuccessStatusCodes = new() { 200, 201, "2xx" },
    BackoffType = BackoffType.Exponential,
    RetryDelays = new() { 1, 5, 30 },
    CircuitFailureThreshold = 5,
    CircuitSuccessThreshold = 2,
    CircuitCooldownSeconds = 60
});

Console.WriteLine($"Endpoint secret: {endpoint.Secret}");

// Update endpoint
var updated = await client.Endpoints.UpdateAsync("ep_123", new UpdateEndpointRequest
{
    IsDisabled = false,
    RateLimitPerSecond = 100
});

// An unset member is omitted from the body, so null cannot also mean "reset this
// setting". Name the field in Clear to send an explicit null for it:
await client.Endpoints.UpdateAsync("ep_123", new UpdateEndpointRequest
{
    Clear = { EndpointField.RetryDelays }
});

// Rotate secret (the old one keeps signing for an hour, or gracePeriodSeconds: 0..86400)
var newSecret = await client.Endpoints.RotateSecretAsync("ep_123");
var slowRotate = await client.Endpoints.RotateSecretAsync("ep_123", gracePeriodSeconds: 86400);

// Filtered list
var openCircuits = await client.Endpoints.ListAsync(new EndpointListOptions { CircuitState = "open" });

// Get statistics
var stats = await client.Endpoints.GetStatsAsync("ep_123");
Console.WriteLine($"Success rate: {stats.SuccessRate:P}");
```

`SuccessStatusCodes` entries are either an exact code (`200`) or a wildcard string (`"2xx"`);
both serialize correctly. `Headers` is always sent as `[{"name": ..., "value": ...}]`.

**Deprecated endpoint members.** These were never accepted by the API - it silently stripped
them, so the endpoint came back configured differently from what was asked for. They are still
present so existing code compiles, but they are never sent:

| Deprecated | Use instead |
|------------|-------------|
| `FilterTypes` | subscriptions (`client.Subscriptions`) |
| `RateLimit` | `RateLimitPerSecond` (a value set on `RateLimit` is still sent as `rateLimitPerSecond`) |
| `RateLimitPeriod` | nothing - endpoint rate limits are always per second |
| `Metadata` | nothing - endpoints have no metadata |

### Webhook Signature Verification

```csharp
using System.Text.Json;
using Hookbase.Webhooks;

// Initialize verifier with your signing secret
var verifier = new WebhookVerifier("whsec_your_secret_here");

// In your webhook endpoint
app.MapPost("/webhooks", async (HttpRequest request) =>
{
    var payload = await new StreamReader(request.Body).ReadToEndAsync();
    var headers = request.Headers.ToDictionary(
        h => h.Key,
        h => h.Value.ToString()
    );

    try
    {
        // Verify and deserialize in one step
        var webhook = verifier.Verify<JsonElement>(payload, headers);

        Console.WriteLine($"Event type: {webhook.GetProperty("type").GetString()}");
        // Process webhook...

        return Results.Ok();
    }
    catch (WebhookVerificationException ex)
    {
        Console.WriteLine($"Verification failed: {ex.Message}");
        return Results.Unauthorized();
    }
});

// To check only the signature, whatever the body (JSON or not), use
// verifier.VerifySignature(payload, headers), which returns a bool.
```

Verify the body exactly as received: a body that was parsed and re-serialized no longer matches
the signature.

#### Ed25519 (`v1a`) endpoints

An endpoint created with `SignatureScheme = "ed25519"` is signed with an Ed25519 private key that
Hookbase keeps; you get only its public key (`whpk_...`). Pass that to the same verifier, which then
checks the `v1a,` signatures instead of `v1,` ones, with the same timestamp tolerance:

```csharp
using System.Text.Json;

var endpoint = await client.Endpoints.CreateAsync(new CreateEndpointRequest
{
    ApplicationId = "app_123",
    Url = "https://customer.example.com/webhooks",
    SignatureScheme = "ed25519",
});

var verifier = new WebhookVerifier(endpoint.PublicKey!); // "whpk_..."
var webhook = verifier.Verify<JsonElement>(payload, headers);
```

`RotateSecretAsync` on an Ed25519 endpoint returns a new `PublicKey` (and the old one as
`PreviousPublicKey`) instead of a `Secret`. Until `PreviousSecretExpiresAt` every delivery carries
signatures from both keys, so a receiver holding either one keeps verifying.

#### Generating test headers

`GenerateTestHeaders` signs a payload the way Hookbase does, so you can test your webhook handler
without a real delivery. It returns `webhook-id`, `webhook-timestamp` and `webhook-signature`:

```csharp
using System.Text.Json;

var verifier = new WebhookVerifier("whsec_your_secret_here");
var payload = "{\"type\":\"order.created\"}";

Dictionary<string, string> headers = verifier.GenerateTestHeaders(payload);
// Optional: GenerateTestHeaders(payload, webhookId: "msg_test_1", timestamp: DateTimeOffset.UtcNow)

verifier.Verify<JsonElement>(payload, headers); // passes
```

Signing an Ed25519 (`v1a`) header needs the private key, which Hookbase never returns. For tests,
use a key pair you generate yourself and pass the secret key as `whsk_` + base64 of the 32-byte
seed followed by the 32-byte public key (the same layout the server and the Python and Java SDKs
use). A `whsk_` verifier verifies with the public half and can sign; the matching `whpk_` key
verifies the same headers. A verifier built from a `whpk_` public key cannot sign, so
`GenerateTestHeaders` throws `InvalidOperationException`.

```csharp
using System.Text.Json;

var signer = new WebhookVerifier("whsk_..."); // your own test key
var headers = signer.GenerateTestHeaders(payload); // "v1a,..." signature
new WebhookVerifier("whpk_...").Verify<JsonElement>(payload, headers);
```

### Error Handling

```csharp
using Hookbase.Exceptions;

try
{
    var source = await client.Sources.GetAsync("invalid_id");
}
catch (NotFoundException ex)
{
    Console.WriteLine($"Source not found: {ex.Message}");
    Console.WriteLine($"Request ID: {ex.RequestId}");
}
catch (ValidationException ex)
{
    Console.WriteLine("Validation failed:");
    if (ex.FieldErrors != null)
    {
        foreach (var (field, errors) in ex.FieldErrors)
        {
            Console.WriteLine($"  {field}: {string.Join(", ", errors)}");
        }
    }
}
catch (RateLimitException ex)
{
    Console.WriteLine($"Rate limited. Retry after: {ex.RetryAfter} seconds");
}
catch (HookbaseApiException ex)
{
    // Code is the API's machine-readable code (FEATURE_DISABLED, LIMIT_EXCEEDED ...), or null
    Console.WriteLine($"API error {ex.StatusCode} ({ex.Code}): {ex.Message}");
}
catch (HookbaseTimeoutException ex)
{
    Console.WriteLine($"Timed out: {ex.Message}");
}
catch (HookbaseNetworkException ex)
{
    Console.WriteLine($"Network failure: {ex.InnerException?.Message}");
}
catch (HookbaseException ex)
{
    Console.WriteLine($"SDK error: {ex.Message}");
}
```

## Exception Types

| Exception | Status Code | Description |
|-----------|-------------|-------------|
| `ValidationException` | 400, 422 | Request validation failed |
| `AuthenticationException` | 401 | Invalid or missing API key |
| `ForbiddenException` | 403 | Insufficient permissions |
| `NotFoundException` | 404 | Resource not found |
| `ConflictException` | 409 | Resource conflict (duplicate) |
| `RateLimitException` | 429 | Rate limit exceeded |
| `HookbaseApiException` | Any other non-2xx | 5xx, and any 4xx without its own type above. The base class of all the API exceptions above; carries `StatusCode`, `Code`, `RequestId` and `Details` |
| `HookbaseTimeoutException` | - | The request ran past `Timeout` (after any retries); `InnerException` is the `TaskCanceledException` |
| `HookbaseNetworkException` | - | DNS, connection or TLS failure (after any retries); `InnerException` is the `HttpRequestException` |
| `WebhookVerificationException` | - | Webhook signature verification failed |

All of these derive from `HookbaseException`.

## Pagination

### Offset-based (Inbound Resources)

Sources, Destinations, Routes, Transforms, Filters and cron jobs take `page`/`pageSize`.
Deliveries take `limit`/`offset`. Schemas return every schema in one page:

```csharp
var page1 = await client.Sources.ListAsync(page: 1, pageSize: 20);
Console.WriteLine($"Page {page1.Page} of {page1.TotalPages}");

if (page1.HasMore)
{
    var page2 = await client.Sources.ListAsync(page: 2, pageSize: 20);
}

// Or use auto-pagination
await foreach (var source in client.Sources.ListAllAsync())
{
    // Automatically fetches all pages
}
```

### Cursor-based (Outbound Resources)

Applications, Endpoints, EventTypes, Subscriptions, Messages and the DLQ (events use
`Events.ListPageAsync`, also cursor-based):

```csharp
var page1 = await client.Applications.ListAsync(limit: 50);

if (page1.HasMore && page1.NextCursor != null)
{
    var page2 = await client.Applications.ListAsync(cursor: page1.NextCursor);
}

// Or use auto-pagination
await foreach (var app in client.Applications.ListAllAsync())
{
    // Automatically follows cursors
}
```

## Advanced Usage

### Upgrading to 5.1

- **Timeout with your own `HttpClient`.** The SDK no longer overwrites `HttpClient.Timeout`. The
  effective timeout is now the shorter of `HookbaseClientOptions.Timeout` (default 30 seconds) and
  your client's `Timeout` (100 seconds by default). Raise both for long requests.
- **`BaseUrl` path prefixes are honoured.** Paths are appended to `BaseUrl` instead of replacing
  its path, so a `BaseUrl` of `https://api.hookbase.app/api` must become `https://api.hookbase.app`.
- **A bad `BaseUrl` throws `ArgumentException`** when the client is created, unless it is an
  absolute `http` or `https` URL.
- **429 is retried for POSTs too**, including those without an idempotency key.
- **Timeouts and network failures** throw `HookbaseTimeoutException` and
  `HookbaseNetworkException`. Both derive from `HookbaseException`, so existing catch blocks still
  work. API errors gain `Code`.
- **`Messages` no longer needs the organization ID.** New overloads without it resolve the
  organization from the API key; the old ones still work.
- **Destination types** are read and written under the API's names (`azure_blob`, `sqs`,
  `eventbridge`, `servicebus`, `pubsub`, `oci_queue`). `AzureBlob` used to be sent as `azureBlob`,
  and lists holding a queue destination failed to parse. An unknown type reads as `null`.
- **Status filters** (`InboundEventStatus`, `DeliveryStatus`) gained the statuses the API uses
  (`Pending`, `Partial`, `Duplicate`, `NoRoutes`; `FailedOver`, `SchemaFailed`, `Throttled`) and
  are sent in snake_case. The members that matched nothing are marked `[Obsolete]`.
- **Source changes in models that never worked.** `ValidateSchemaResult.Errors` is now a list of
  `SchemaValidationError` (`Path`, `Message`); as `List<string>` it threw on any failed validation.
  The flat `DashboardData` properties (`EventsReceived` ...) are now read-only and computed from
  `Overview`; they were always 0 before.
- **Calls that failed against the API now work:** `Transforms.UpdateAsync` (reads the transform
  back after the update), `Schemas.ValidateAsync` (right route and body), route bulk delete and
  update (send `ids`), `Tunnels.CreateAsync` (returns `AuthToken`, `TunnelUrl`, `WsUrl`),
  `Filters.TestAsync` (`Matched`, `Results`, `Logic`), `Events.DebugAsync` (`CurlCommand`,
  request headers, payload).

### Custom HttpClient and IHttpClientFactory

You can pass your own `HttpClient`, for example to add a handler (logging, a proxy) or to use
`IHttpClientFactory`. The SDK never modifies it: `BaseAddress`, `Timeout` and
`DefaultRequestHeaders` stay as you set them, and the SDK never disposes it. Each request is sent
to an absolute URL built from `BaseUrl`, with its own `Authorization` and `User-Agent` headers, so
one `HttpClient` can be shared by several `HookbaseClient`s with different API keys.

The request timeout is `HookbaseClientOptions.Timeout` (seconds, default 30), applied per attempt.
The `HttpClient`'s own `Timeout` (100 seconds unless you change it) still applies too, and whichever
is shorter wins, so raise both for long requests:

```csharp
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

var client = new HookbaseClient(new HookbaseClientOptions
{
    ApiKey = "whr_key",
    Timeout = 120,          // the SDK's per-request timeout
    HttpClient = httpClient // not modified or disposed by the SDK
});
```

With `IHttpClientFactory` (ASP.NET Core):

```csharp
builder.Services.AddHttpClient("hookbase");
builder.Services.AddSingleton(sp => new HookbaseClient(new HookbaseClientOptions
{
    ApiKey = builder.Configuration["Hookbase:ApiKey"]!,
    HttpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("hookbase")
}));
```

A singleton `HookbaseClient` keeps the `HttpClient` it was created with, so the factory's handler
rotation does not reach it. If DNS changes matter, give the named client a primary handler that
recycles connections:

```csharp
builder.Services.AddHttpClient("hookbase")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    });
```

Cancelling the `CancellationToken` you pass to a method throws `OperationCanceledException` (a
`TaskCanceledException`), and the request is not retried. A timeout throws
`HookbaseTimeoutException` ("Request failed: Request timed out after N seconds
(HookbaseClientOptions.Timeout)") once the retries are used up.

### Retries

Failed requests are retried up to `MaxRetries` times (default 3) on timeouts, network errors, 429
and 500/502/503/504, with exponential backoff and jitter (1, 2, 4 ... seconds, at most 10), or the
`Retry-After` delay (seconds or an HTTP date) when the API sends one. A 429 without `Retry-After`
backs off the same way. Other 4xx responses are never retried.

A 429 is retried for every method, POST included, because the API rejects a rate-limited request
before doing any work. A 5xx, network error or timeout may come after the API has already acted, so
a POST is retried on those only when it carries an idempotency key; a POST without one is not.
`Messages.SendAsync` always sends a key (`IdempotencyKey`, else the deprecated `EventId`, else
one generated per call), so sends are retried safely.

### Idempotency Keys

```csharp
// A send with the same IdempotencyKey is not delivered twice. The key is IdempotencyKey, else the
// deprecated EventId, else one the SDK generates per call, so its own retries (on timeouts, 429
// and 5xx) cannot duplicate a send. Other POST requests carry no key: they are retried on 429
// only, because repeating them after a 5xx or timeout is not safe.
await client.Messages.SendAsync(new SendMessageRequest
{
    IdempotencyKey = "unique_event_id_123",
    EventType = "payment.succeeded",
    Payload = new Dictionary<string, object> { ["paymentId"] = "pay_123" }
});
```

## API Coverage

### Inbound (Webhook Relay)

| Resource | Methods |
|----------|---------|
| **Sources** | List (paged and `ListAllAsync`), get, create, update, delete, bulk delete, rotate and reveal secret, verify token get/rotate, pending verification get/adopt, export, import |
| **Providers** | Public catalog: list, get, event types and sample payloads |
| **Destinations** | List (paged and `ListAllAsync`), get, create, update, delete, bulk delete, test, export, import |
| **Routes** | List, get, create, update, delete, bulk delete, bulk update, export, import, circuit breaker status/reset/config |
| **Events** | List (`ListPageAsync`, `ListAllAsync`), get, debug |
| **Deliveries** | List, get, replay |
| **Transforms** | List, get, create, update, delete, test |
| **Filters** | List, get, create, update, delete, test |
| **Schemas** | List, get, create, update, delete, validate |
| **Tunnels** | List, get, create (returns the one-time auth token), bulk delete |
| **CronJobs** | List, get, create, update, delete, trigger, list executions, list and create groups |
| **Analytics** | Dashboard (range, time zone, source or destination scope) |
| **ApiKeys** | List |

### Outbound (Customer Webhooks)

| Resource | Methods |
|----------|---------|
| **Applications** | List (cursor and `ListAllAsync`), get, create, update, delete, get-or-create |
| **Endpoints** | List (with filters), get, create, update, delete, rotate secret (with grace period), stats |
| **EventTypes** | List (with filters), get, create, update, delete |
| **Subscriptions** | List, get, create, update, delete, bulk subscribe |
| **Messages** | Send, list, get, list attempts, replay, stats summary, export |
| **PortalTokens** | Create, list, revoke |
| **DLQ** | List (with filters), stats, retry and delete one message or in bulk |

### Utilities

- **WebhookVerifier** - HMAC-SHA256 (`v1`) and Ed25519 (`v1a`) signature verification, and
  `GenerateTestHeaders` for tests (HMAC, or Ed25519 with a `whsk_` key)

## Requirements

- .NET 8.0 or later
- C# 12.0 language features

## Contributing

Contributions are welcome. When adding or changing an endpoint:
1. Follow the pattern in `SourcesResource.cs` and `ApplicationsResource.cs`
2. Update response wrapper types as needed
3. Add XML documentation and a contract test under `tests/Hookbase.Tests`
4. Run `dotnet build` and `dotnet test`

## License

MIT License - see [LICENSE](LICENSE) for details.

## Links

- [Hookbase Documentation](https://www.hookbase.app/docs)
- [API Reference](https://www.hookbase.app/docs/receive/api)
- [GitHub Repository](https://github.com/HookbaseApp/hookbase-dotnet)
- [NuGet Package](https://www.nuget.org/packages/Hookbase)
- [Report Issues](https://github.com/HookbaseApp/hookbase-dotnet/issues)

## Support

- Documentation: https://www.hookbase.app/docs
- Email: support@hookbase.app
- Twitter: [@hookbaseapp](https://twitter.com/hookbaseapp)

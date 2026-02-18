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
- **Webhook verification** - Built-in HMAC-SHA256 signature verification
- **Retry logic** - Exponential backoff with jitter for failed requests
- **Full API coverage** - Support for all Hookbase API endpoints

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

foreach (var source in sources.Data)
{
    Console.WriteLine($"{source.Name} ({source.Slug})");
}

// Create a new source
var newSource = await client.Sources.CreateAsync(new CreateSourceRequest
{
    Name = "GitHub Webhooks",
    Slug = "github-prod",
    Provider = SourceProvider.Github,
    VerifySignature = true
});

Console.WriteLine($"Created source: {newSource.Id}");
Console.WriteLine($"Signing secret: {newSource.SigningSecret}");

// Get a source
var source = await client.Sources.GetAsync("source_123");

// Update a source
var updated = await client.Sources.UpdateAsync("source_123", new UpdateSourceRequest
{
    Name = "GitHub Production",
    IsActive = true
});

// Rotate signing secret
var rotated = await client.Sources.RotateSecretAsync("source_123");
Console.WriteLine($"New secret: {rotated.SigningSecret}");

// Delete a source
await client.Sources.DeleteAsync("source_123");
```

### Auto-pagination with IAsyncEnumerable

```csharp
// Iterate through ALL sources automatically
await foreach (var source in client.Sources.ListAllAsync())
{
    Console.WriteLine($"Processing: {source.Name}");
    // SDK automatically handles pagination
}

// With filters
await foreach (var source in client.Sources.ListAllAsync(search: "stripe", isActive: true))
{
    Console.WriteLine($"Active source: {source.Name}");
}
```

### Working with Applications (Outbound Webhooks)

```csharp
// List applications (cursor-based pagination)
var apps = await client.Applications.ListAsync(limit: 50);

foreach (var app in apps.Data)
{
    Console.WriteLine($"{app.Name} - {app.Uid}");
}

if (apps.HasMore)
{
    var nextPage = await client.Applications.ListAsync(cursor: apps.NextCursor);
}

// Create application
var app = await client.Applications.CreateAsync(new CreateApplicationRequest
{
    Name = "Customer Portal",
    Uid = "customer_12345",
    Metadata = new Dictionary<string, object>
    {
        ["plan"] = "pro",
        ["region"] = "us-east"
    }
});

// Get or create (idempotent)
var appOrCreate = await client.Applications.GetOrCreateAsync(new GetOrCreateApplicationRequest
{
    Uid = "customer_12345",
    Name = "Customer Portal",
    Metadata = new Dictionary<string, object> { ["created"] = "2024" }
});
```

### Sending Webhook Messages

```csharp
// Send a message to all subscribed endpoints
var response = await client.Messages.SendAsync("app_123", new SendMessageRequest
{
    EventType = "payment.succeeded",
    Payload = new Dictionary<string, object>
    {
        ["amount"] = 4999,
        ["currency"] = "usd",
        ["customer_id"] = "cust_123"
    },
    EventId = "evt_unique_123" // Optional idempotency key
});

Console.WriteLine($"Message ID: {response.MessageId}");
Console.WriteLine($"Sent to {response.OutboundMessages.Count} endpoints");

// Send to specific endpoints only
var targeted = await client.Messages.SendAsync("app_123", new SendMessageRequest
{
    EventType = "order.created",
    Payload = new Dictionary<string, object> { ["order_id"] = "ord_456" },
    EndpointIds = new List<string> { "ep_123", "ep_456" }
});
```

### Managing Endpoints

```csharp
// Create endpoint
var endpoint = await client.Endpoints.CreateAsync("app_123", new CreateEndpointRequest
{
    Url = "https://customer.example.com/webhooks",
    Description = "Production webhook endpoint",
    FilterTypes = new List<string> { "payment.*", "subscription.*" }
});

Console.WriteLine($"Endpoint secret: {endpoint.Secret}");

// Update endpoint
await client.Endpoints.UpdateAsync("app_123", "ep_123", new UpdateEndpointRequest
{
    IsDisabled = false,
    RateLimit = 100,
    RateLimitPeriod = 60
});

// Rotate secret
var newSecret = await client.Endpoints.RotateSecretAsync("app_123", "ep_123");

// Get statistics
var stats = await client.Endpoints.GetStatsAsync("app_123", "ep_123");
Console.WriteLine($"Success rate: {stats.SuccessRate:P}");
```

### Webhook Signature Verification

```csharp
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
        var webhook = verifier.Verify<WebhookPayload>(payload, headers);

        Console.WriteLine($"Event type: {webhook.EventType}");
        // Process webhook...

        return Results.Ok();
    }
    catch (WebhookVerificationException ex)
    {
        Console.WriteLine($"Verification failed: {ex.Message}");
        return Results.Unauthorized();
    }
});

// Or verify without deserializing
if (verifier.VerifySignature(payload, headers))
{
    // Signature is valid
}
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
    Console.WriteLine($"API error {ex.StatusCode}: {ex.Message}");
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
| `HookbaseApiException` | 5xx | Server error |
| `WebhookVerificationException` | - | Webhook signature verification failed |

## Pagination

### Offset-based (Inbound Resources)

Sources, Destinations, Routes, Events, Deliveries, Transforms, Filters, Schemas:

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

Applications, Endpoints, EventTypes, Subscriptions, Messages:

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

### Custom HttpClient

```csharp
var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(120)
};

var client = new HookbaseClient(new HookbaseClientOptions
{
    ApiKey = "whr_key",
    HttpClient = httpClient // SDK will not dispose this
});
```

### Idempotency Keys

```csharp
// Messages support idempotency via EventId
await client.Messages.SendAsync("app_123", new SendMessageRequest
{
    EventId = "unique_event_id_123", // Prevents duplicate sends
    EventType = "payment.succeeded",
    Payload = data
});
```

## API Coverage

### Inbound (Webhook Relay)

- ✅ **Sources** - Fully implemented with pagination, export, import
- ⏳ **Destinations** - CRUD operations (stub implementation)
- ⏳ **Routes** - CRUD, circuit breaker, bulk operations (stub)
- ⏳ **Events** - List, get, debug (stub)
- ⏳ **Deliveries** - List, get, replay (stub)
- ⏳ **Transforms** - JSONata transforms with testing (stub)
- ⏳ **Filters** - Filter expressions with testing (stub)
- ⏳ **Schemas** - JSON schema validation (stub)

### Outbound (Customer Webhooks)

- ✅ **Applications** - Fully implemented with cursor pagination
- ⏳ **Endpoints** - CRUD, secret rotation, stats (stub)
- ⏳ **EventTypes** - CRUD, archive/unarchive (stub)
- ⏳ **Subscriptions** - CRUD, bulk operations (stub)
- ⏳ **Messages** - Send, list, replay, stats (stub)
- ⏳ **PortalTokens** - Create, list, revoke (stub)
- ⏳ **DLQ** - List, retry, delete (stub)

### Utilities

- ✅ **WebhookVerifier** - HMAC-SHA256 signature verification

## Requirements

- .NET 8.0 or later
- C# 12.0 language features

## Contributing

This SDK is in active development. Contributions are welcome!

To implement remaining endpoints:
1. See `DEVELOPMENT.md` for implementation patterns
2. Follow the pattern in `SourcesResource.cs` and `ApplicationsResource.cs`
3. Update response wrapper types as needed
4. Add comprehensive XML documentation

## License

MIT License - see [LICENSE](LICENSE) for details.

## Links

- [Hookbase Documentation](https://docs.hookbase.app)
- [API Reference](https://docs.hookbase.app/api-reference)
- [GitHub Repository](https://github.com/hookbase/hookbase-dotnet)
- [NuGet Package](https://www.nuget.org/packages/Hookbase)
- [Report Issues](https://github.com/hookbase/hookbase-dotnet/issues)

## Support

- Documentation: https://docs.hookbase.app
- Email: support@hookbase.app
- Twitter: [@hookbaseapp](https://twitter.com/hookbaseapp)

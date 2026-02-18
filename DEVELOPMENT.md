# Hookbase .NET SDK - Development Guide

This guide explains how to complete the stub implementations in the SDK.

## Project Structure

```
src/Hookbase/
├── Http/                    # HTTP client infrastructure
│   ├── IApiClient.cs       # HTTP client interface
│   └── ApiClient.cs        # HTTP client with retry logic
├── Models/                  # DTOs and request/response models
│   ├── Common/             # Shared types, enums, pagination
│   ├── Sources/            # Source models
│   ├── Applications/       # Application models
│   └── ...                 # Other resource models
├── Resources/              # Resource client classes
│   ├── BaseResource.cs     # Base class with helper methods
│   ├── SourcesResource.cs  # ✅ Complete implementation (reference)
│   ├── ApplicationsResource.cs # ✅ Complete implementation (reference)
│   └── ...                 # Other resources (stubs)
├── Pagination/             # Pagination wrappers
│   ├── OffsetPage.cs       # Offset-based pagination
│   └── CursorPage.cs       # Cursor-based pagination
├── Exceptions/             # Custom exception types
├── Webhooks/               # Webhook signature verification
└── HookbaseClient.cs       # Main client class
```

## Implementing a Resource

### Step 1: Understand the API Response Format

**Inbound resources** (Sources, Destinations, Routes, Events, Deliveries):
- Single: `{ "source": { ... } }` or `{ "destination": { ... } }`
- List: `{ "sources": [...], "pagination": { "total": 100, "page": 1, "pageSize": 20 } }`
- Uses offset-based pagination

**Outbound resources** (Applications, Endpoints, Messages):
- Single: `{ "data": { ... } }`
- List: `{ "data": [...], "pagination": { "hasMore": true, "nextCursor": "..." } }`
- Uses cursor-based pagination

### Step 2: Create Response Wrapper Types

Add private record types at the bottom of your resource class:

```csharp
// For single resource
private record GetDestinationResponse
{
    public required Destination Destination { get; init; }
}

// For list with offset pagination
private record ListDestinationsResponse
{
    public required List<Destination> Destinations { get; init; }
    public required PaginationInfo Pagination { get; init; }
}

// For list with cursor pagination
private record ListEndpointsResponse
{
    public required List<Endpoint> Data { get; init; }
    public required CursorPaginationInfo Pagination { get; init; }
}
```

### Step 3: Implement List Method (Offset-based)

```csharp
public async Task<OffsetPage<Destination>> ListAsync(
    int page = 1,
    int pageSize = 20,
    string? search = null,
    bool? isActive = null,
    CancellationToken cancellationToken = default)
{
    var queryParams = new Dictionary<string, string>
    {
        ["page"] = page.ToString(),
        ["pageSize"] = pageSize.ToString()
    };

    if (search != null) queryParams["search"] = search;
    if (isActive.HasValue) queryParams["isActive"] = isActive.Value.ToString().ToLower();

    var response = await ApiClient.RequestAsync<ListDestinationsResponse>(
        HttpMethod.Get,
        "/api/destinations",
        queryParams: queryParams,
        cancellationToken: cancellationToken
    );

    return new OffsetPage<Destination>
    {
        Data = response.Destinations,
        Total = response.Pagination.Total,
        Page = response.Pagination.Page,
        PageSize = response.Pagination.PageSize
    };
}
```

### Step 4: Implement List Method (Cursor-based)

```csharp
public async Task<CursorPage<Endpoint>> ListAsync(
    string applicationId,
    int limit = 50,
    string? cursor = null,
    CancellationToken cancellationToken = default)
{
    var queryParams = new Dictionary<string, string>
    {
        ["limit"] = limit.ToString()
    };

    if (cursor != null) queryParams["cursor"] = cursor;

    var response = await ApiClient.RequestAsync<ListEndpointsResponse>(
        HttpMethod.Get,
        $"/api/webhook-applications/{Uri.EscapeDataString(applicationId)}/endpoints",
        queryParams: queryParams,
        cancellationToken: cancellationToken
    );

    return new CursorPage<Endpoint>
    {
        Data = response.Data,
        HasMore = response.Pagination.HasMore,
        NextCursor = response.Pagination.NextCursor
    };
}
```

### Step 5: Implement ListAllAsync with IAsyncEnumerable

**For offset-based:**

```csharp
public async IAsyncEnumerable<Destination> ListAllAsync(
    string? search = null,
    bool? isActive = null,
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    int page = 1;
    bool hasMore = true;

    while (hasMore)
    {
        var result = await ListAsync(page, 100, search, isActive, cancellationToken);
        foreach (var item in result.Data)
        {
            yield return item;
        }
        hasMore = result.HasMore;
        page++;
    }
}
```

**For cursor-based:**

```csharp
public async IAsyncEnumerable<Endpoint> ListAllAsync(
    string applicationId,
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    string? cursor = null;
    bool hasMore = true;

    while (hasMore)
    {
        var result = await ListAsync(applicationId, 100, cursor, cancellationToken);
        foreach (var item in result.Data)
        {
            yield return item;
        }
        hasMore = result.HasMore;
        cursor = result.NextCursor;
    }
}
```

### Step 6: Implement CRUD Operations

**Get:**

```csharp
public async Task<Destination> GetAsync(string id, CancellationToken cancellationToken = default)
{
    var response = await ApiClient.RequestAsync<GetDestinationResponse>(
        HttpMethod.Get,
        $"/api/destinations/{Uri.EscapeDataString(id)}",
        cancellationToken: cancellationToken
    );

    return response.Destination;
}
```

**Create:**

```csharp
public async Task<Destination> CreateAsync(
    CreateDestinationRequest request,
    CancellationToken cancellationToken = default)
{
    var response = await ApiClient.RequestAsync<GetDestinationResponse>(
        HttpMethod.Post,
        "/api/destinations",
        body: request,
        cancellationToken: cancellationToken
    );

    return response.Destination;
}
```

**Update:**

```csharp
public async Task<Destination> UpdateAsync(
    string id,
    UpdateDestinationRequest request,
    CancellationToken cancellationToken = default)
{
    var response = await ApiClient.RequestAsync<GetDestinationResponse>(
        HttpMethod.Patch,
        $"/api/destinations/{Uri.EscapeDataString(id)}",
        body: request,
        cancellationToken: cancellationToken
    );

    return response.Destination;
}
```

**Delete:**

```csharp
public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
{
    await ApiClient.RequestAsync<object>(
        HttpMethod.Delete,
        $"/api/destinations/{Uri.EscapeDataString(id)}",
        cancellationToken: cancellationToken
    );
}
```

### Step 7: Add XML Documentation

```csharp
/// <summary>
/// Resource client for managing webhook delivery destinations.
/// </summary>
public class DestinationsResource : BaseResource
{
    /// <summary>
    /// List all destinations with offset-based pagination.
    /// </summary>
    /// <param name="page">Page number (1-indexed)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="search">Search query</param>
    /// <param name="isActive">Filter by active status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Page of destinations</returns>
    public async Task<OffsetPage<Destination>> ListAsync(...)
    {
        // ...
    }
}
```

## API Endpoint Reference

Check the corresponding API route files in `/root/saas/hookbase/api/src/routes/` to understand:
- Endpoint paths
- Query parameters
- Request/response formats
- Special operations (test, export, rotate-secret, etc.)

Or reference the Node.js SDK in `/root/saas/hookbase/sdk/src/resources/`.

## Testing

Create unit tests in `tests/Hookbase.Tests/`:

```csharp
using Hookbase.Resources;
using Hookbase.Models.Destinations;
using Hookbase.Http;
using Moq;
using FluentAssertions;

public class DestinationsResourceTests
{
    private readonly Mock<IApiClient> _mockApiClient;
    private readonly DestinationsResource _resource;

    public DestinationsResourceTests()
    {
        _mockApiClient = new Mock<IApiClient>();
        _resource = new DestinationsResource(_mockApiClient.Object);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnDestinations()
    {
        // Arrange
        var expectedResponse = new
        {
            Destinations = new List<Destination> { /* ... */ },
            Pagination = new { Total = 1, Page = 1, PageSize = 20 }
        };

        _mockApiClient
            .Setup(x => x.RequestAsync<object>(
                HttpMethod.Get,
                "/api/destinations",
                null,
                It.IsAny<Dictionary<string, string>>(),
                null,
                default))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _resource.ListAsync();

        // Assert
        result.Data.Should().HaveCount(1);
        result.Total.Should().Be(1);
    }
}
```

## Common Patterns

### Special Operations

**Test endpoint:**

```csharp
public async Task<TestDestinationResult> TestAsync(
    string id,
    CancellationToken cancellationToken = default)
{
    return await ApiClient.RequestAsync<TestDestinationResult>(
        HttpMethod.Post,
        $"/api/destinations/{Uri.EscapeDataString(id)}/test",
        cancellationToken: cancellationToken
    );
}
```

**Rotate secret:**

```csharp
public async Task<DestinationWithSecret> RotateSecretAsync(
    string id,
    CancellationToken cancellationToken = default)
{
    var response = await ApiClient.RequestAsync<RotateSecretResponse>(
        HttpMethod.Post,
        $"/api/destinations/{Uri.EscapeDataString(id)}/rotate-secret",
        cancellationToken: cancellationToken
    );

    return response.Destination;
}
```

**Export/Import:**

```csharp
public async Task<List<Destination>> ExportAsync(CancellationToken cancellationToken = default)
{
    var response = await ApiClient.RequestAsync<ExportResponse>(
        HttpMethod.Get,
        "/api/destinations/export",
        cancellationToken: cancellationToken
    );

    return response.Destinations;
}

public async Task<ImportResult> ImportAsync(
    List<CreateDestinationRequest> destinations,
    CancellationToken cancellationToken = default)
{
    return await ApiClient.RequestAsync<ImportResult>(
        HttpMethod.Post,
        "/api/destinations/import",
        body: new { destinations },
        cancellationToken: cancellationToken
    );
}
```

## Building and Testing

```bash
# Build
dotnet build

# Run tests
dotnet test

# Create NuGet package
dotnet pack -c Release
```

## Resources to Implement

Priority order:

1. **DestinationsResource** - Similar to Sources
2. **RoutesResource** - Add circuit breaker endpoints
3. **EndpointsResource** - Cursor pagination + secret rotation
4. **EventTypesResource** - Cursor pagination
5. **SubscriptionsResource** - Bulk operations
6. **MessagesResource** - Send, replay, stats
7. **EventsResource** - List, get, debug
8. **DeliveriesResource** - List, get, replay
9. **TransformsResource** - CRUD + test
10. **FiltersResource** - CRUD + test
11. **SchemasResource** - CRUD + validate
12. **PortalTokensResource** - Create, list, revoke
13. **DlqResource** - List, retry, delete

## Need Help?

- Check reference implementations: `SourcesResource.cs`, `ApplicationsResource.cs`
- Check API routes: `/root/saas/hookbase/api/src/routes/`
- Check Node.js SDK: `/root/saas/hookbase/sdk/node-sdk/src/resources/`
- Check memory notes: `/root/.claude/projects/-root-saas-hookbase/memory/MEMORY.md`

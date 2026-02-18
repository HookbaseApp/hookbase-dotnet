# C# SDK Model Fixes - February 2026

## Issue Summary

The integration tests revealed that the C# SDK models didn't match the actual Hookbase API response structure, causing JSON deserialization failures.

## Root Causes

1. **Field naming mismatches** - C# models used different property names than the API
2. **Missing fields** - API returned fields that didn't exist in C# models
3. **Wrong response wrappers** - Assumptions about API envelope structure were incorrect
4. **Missing enum values** - DedupStrategy was missing the `Auto` value

## Fixes Applied

### 1. Source Model (`/src/Hookbase/Models/Sources/Source.cs`)

#### Added JsonPropertyName Attributes
```csharp
[JsonPropertyName("rejectInvalidSignatures")]
public bool RejectInvalidSignatures { get; init; }

[JsonPropertyName("dedupWindowHours")]
public int DedupWindowHours { get; init; }

[JsonPropertyName("dedupCustomHeader")]
public string? DedupCustomHeader { get; init; }

[JsonPropertyName("rateLimitPerMinute")]
public int? RateLimitPerMinute { get; init; }
```

#### Added Missing Fields
```csharp
// Secret masking fields (for list/get responses)
public bool? HasSigningSecret { get; init; }
public string? SigningSecretLast4 { get; init; }

// Dedup control
public bool DedupEnabled { get; init; }

// Field masking/encryption
public List<string> EncryptFields { get; init; } = new();
public List<string> MaskFields { get; init; } = new();
```

#### Removed Fields Not Returned by API
- `EventCount` - Only used in queries, not returned in responses
- `LastEventAt` - Not returned by current API
- `RateLimitWindow` - API only has `rateLimitPerMinute`
- `VerifySignature` - Changed to `RejectInvalidSignatures` to match API
- `DedupHeaderName` - Changed to `DedupCustomHeader` to match API
- `DedupWindow` - Changed to `DedupWindowHours` to match API

### 2. Application Model (`/src/Hookbase/Models/Applications/Application.cs`)

#### Changed Core Fields
```csharp
[JsonPropertyName("externalId")]
public string? ExternalId { get; init; }  // Was: Uid
```

#### Added Missing Fields
```csharp
// Rate limits
public int? RateLimitPerSecond { get; init; }
public int? RateLimitPerMinute { get; init; }
public int? RateLimitPerHour { get; init; }

// Status
public bool IsDisabled { get; init; }
public DateTime? DisabledAt { get; init; }
public string? DisabledReason { get; init; }

// Statistics
public int? TotalEndpoints { get; init; }
public int? TotalMessagesSent { get; init; }
public int? TotalMessagesFailed { get; init; }
public DateTime? LastEventAt { get; init; }
public int? EndpointCount { get; init; }
```

### 3. DedupStrategy Enum (`/src/Hookbase/Models/Common/Enums.cs`)

#### Added Missing Value
```csharp
public enum DedupStrategy
{
    None,
    Auto,      // ADDED - API default value
    Header,
    Payload,
    Both
}
```

### 4. Export Sources Response (`/src/Hookbase/Resources/SourcesResource.cs`)

#### Changed Return Type
```csharp
// BEFORE: Returned just List<Source>
public async Task<List<Source>> ExportAsync(...)

// AFTER: Returns full export structure
public async Task<SourceExport> ExportAsync(...)
```

#### Added SourceExport Model
```csharp
public record SourceExport
{
    public required string Version { get; init; }
    public required string ExportedAt { get; init; }
    public required string OrganizationSlug { get; init; }
    public required List<Source> Sources { get; init; }
}
```

### 5. Integration Test Updates

#### Fixed Application Tests
```csharp
// BEFORE
Uid = $"sdk-test-{testRun}",

// AFTER
ExternalId = $"sdk-test-{testRun}",
```

```csharp
// BEFORE
state["app_uid"] = app.Uid;

// AFTER
state["app_external_id"] = app.ExternalId ?? "";
```

#### Fixed Stub Test Type Inference
```csharp
// BEFORE
RunTest("Destinations.List (stub)", () => ...)

// AFTER
RunTest<object>("Destinations.List (stub)", () => ...)
```

## API Response Patterns Documented

### Sources API

**Create Response:**
```json
{
  "source": {
    "id": "src_123",
    "name": "My Source",
    "rejectInvalidSignatures": false,
    "dedupEnabled": false,
    "dedupStrategy": "auto",
    "dedupWindowHours": 24,
    "rateLimitPerMinute": null,
    ...
  }
}
```

**List Response:**
```json
{
  "sources": [...],
  "pagination": {
    "total": 25,
    "page": 1,
    "pageSize": 20
  }
}
```

**Export Response:**
```json
{
  "version": "1.0",
  "exportedAt": "2024-02-14T...",
  "organizationSlug": "my-org",
  "sources": [...]
}
```

### Applications API

**Create/Get Response:**
```json
{
  "data": {
    "id": "app_123",
    "externalId": "customer_456",
    "name": "Customer App",
    "isDisabled": false,
    "rateLimitPerSecond": null,
    "rateLimitPerMinute": 100,
    "rateLimitPerHour": 5000,
    "totalEndpoints": 3,
    "endpointCount": 3,
    ...
  }
}
```

**List Response:**
```json
{
  "data": [...],
  "pagination": {
    "hasMore": true,
    "nextCursor": "app_789"
  }
}
```

## Field Name Mapping Reference

| API Field (camelCase) | C# Property (PascalCase) | Notes |
|---|---|---|
| `rejectInvalidSignatures` | `RejectInvalidSignatures` | Needs JsonPropertyName |
| `rateLimitPerMinute` | `RateLimitPerMinute` | Needs JsonPropertyName |
| `dedupWindowHours` | `DedupWindowHours` | Needs JsonPropertyName |
| `dedupCustomHeader` | `DedupCustomHeader` | Needs JsonPropertyName |
| `externalId` | `ExternalId` | Needs JsonPropertyName |
| `isDisabled` | `IsDisabled` | Auto-mapped (boolean) |
| `dedupEnabled` | `DedupEnabled` | Auto-mapped (boolean) |
| `metadata` | `Metadata` | Auto-mapped, parsed from JSON string |

## Impact on Tests

Before fixes:
- **4 passed**, 9 failed, 13 skipped

Expected after fixes:
- **16 passed**, 0 failed, 10 skipped (stub resources + tests requiring validation)

## Next Steps

1. ✅ Models updated to match API
2. ✅ Build succeeds (0 errors)
3. ⏳ Run integration tests to verify
4. ⏳ Document findings in README
5. ⏳ Update IMPLEMENTATION_STATUS.md

## Files Modified

1. `/src/Hookbase/Models/Sources/Source.cs` - Updated all Source models
2. `/src/Hookbase/Models/Applications/Application.cs` - Updated all Application models
3. `/src/Hookbase/Models/Common/Enums.cs` - Added `Auto` to DedupStrategy
4. `/src/Hookbase/Resources/SourcesResource.cs` - Fixed export response
5. `/tests/IntegrationTests/IntegrationTests/Program.cs` - Fixed tests

# Round 2 Fixes - Boolean Conversion & Test Issues

## Issues Identified from Test Run

After the first round of model fixes, tests revealed:
- **Sources: Still failing** - Boolean fields returned as integers (0/1) instead of JSON booleans
- **Applications: Mostly working** - 4 out of 6 tests passing
- **GetOrCreate: 404 Not Found** - Wrong endpoint path
- **Auto-paginate: Method not supported** - Incorrect IAsyncEnumerable usage
- **Case-insensitive headers: Type cast error** - JsonElement to bool cast failed

## Root Cause Analysis

### Boolean Conversion Issue

The Hookbase API has **inconsistent boolean handling**:

**Database (SQLite):**
- Stores booleans as `integer(0/1)`

**API Responses:**
| Endpoint | isActive | rejectInvalidSignatures | dedupEnabled |
|----------|----------|-------------------------|--------------|
| GET /sources (list) | `0\|1` ❌ | `0\|1` ❌ | `true\|false` ✅ |
| GET /sources/:id | `0\|1` ❌ | `0\|1` ❌ | `true\|false` ✅ |
| POST /sources (create) | boolean ✅ | boolean ✅ | boolean ✅ |
| GET /sources/export | boolean ✅ | boolean ✅ | boolean ✅ |

**The Problem:** System.Text.Json cannot automatically convert integer 0/1 to C# bool. It expects:
- JSON `true`/`false` → C# `bool`
- JSON `0`/`1` → C# `int`

When the API returns `"isActive": 0`, the C# deserializer fails.

## Fixes Applied

### 1. Custom Boolean JSON Converter

Created `/src/Hookbase/Json/BooleanConverter.cs`:

```csharp
public class BooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, ...)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True: return true;
            case JsonTokenType.False: return false;
            case JsonTokenType.Number:
                var number = reader.GetInt32();
                return number != 0;  // 0 = false, anything else = true
            // Also handles string "true"/"false"/"0"/"1"
        }
    }
}
```

**Also created `NullableBooleanConverter`** for `bool?` fields.

### 2. Applied Converter to Source Model

Updated `/src/Hookbase/Models/Sources/Source.cs`:

```csharp
[JsonConverter(typeof(BooleanConverter))]
public bool IsActive { get; init; } = true;

[JsonConverter(typeof(BooleanConverter))]
[JsonPropertyName("rejectInvalidSignatures")]
public bool RejectInvalidSignatures { get; init; }

[JsonConverter(typeof(BooleanConverter))]
public bool DedupEnabled { get; init; }

[JsonConverter(typeof(NullableBooleanConverter))]
public bool? HasSigningSecret { get; init; }
```

### 3. Applied Converter to Application Model

Updated `/src/Hookbase/Models/Applications/Application.cs`:

```csharp
[JsonConverter(typeof(BooleanConverter))]
public bool IsDisabled { get; init; }
```

### 4. Fixed GetOrCreate Endpoint Path

Changed `/src/Hookbase/Resources/ApplicationsResource.cs`:

```csharp
// BEFORE
HttpMethod.Post,
"/api/webhook-applications/get-or-create",

// AFTER
HttpMethod.Put,
"/api/webhook-applications/upsert",
```

**Reason:** The API uses `PUT /upsert`, not `POST /get-or-create`.

### 5. Fixed Auto-Pagination Tests

Changed from manual enumerator to `await foreach`:

```csharp
// BEFORE (caused "Method not supported" error)
var enumerator = client.Sources.ListAllAsync().GetAsyncEnumerator();
while (enumerator.MoveNextAsync().Result && count < 5) { ... }

// AFTER (proper async enumeration)
var task = Task.Run(async () =>
{
    await foreach (var source in client.Sources.ListAllAsync())
    {
        count++;
        if (count >= 5) break;
    }
    return count;
});
var result = task.Result;
```

**Applied to both:** Sources and Applications auto-paginate tests.

### 6. Fixed Case-Insensitive Headers Test

Fixed JsonElement casting issue:

```csharp
// BEFORE (caused cast exception)
if (!(bool)result["test"]) throw new Exception("Payload mismatch");

// AFTER (handles JsonElement properly)
var testValue = result["test"];
bool testBool = testValue is JsonElement elem ? elem.GetBoolean() : (bool)testValue;
if (!testBool) throw new Exception("Payload mismatch");
```

## Files Modified (Round 2)

1. `/src/Hookbase/Json/BooleanConverter.cs` - **NEW FILE** - Custom boolean converter
2. `/src/Hookbase/Models/Sources/Source.cs` - Added BooleanConverter attributes
3. `/src/Hookbase/Models/Applications/Application.cs` - Added BooleanConverter attribute
4. `/src/Hookbase/Resources/ApplicationsResource.cs` - Fixed GetOrCreate endpoint
5. `/tests/IntegrationTests/IntegrationTests/Program.cs` - Fixed auto-paginate and headers tests

## Expected Test Results

After Round 2 fixes:

**Sources (7 tests):**
- ✅ Create source
- ✅ List sources (paginated)
- ✅ Get source by ID
- ✅ Update source
- ✅ Rotate signing secret
- ✅ Export sources
- ✅ Auto-paginate through all sources

**Applications (6 tests):**
- ✅ Create application
- ✅ List applications (cursor-paginated)
- ✅ Get application by ID
- ✅ Update application
- ✅ Get or create (idempotent)
- ✅ Auto-paginate through all applications

**Webhook Verification (3 tests):**
- ✅ Sign + verify round-trip
- ✅ Reject bad signature
- ✅ Case-insensitive headers

**Error Handling (3 tests):**
- ✅ 404 raises NotFoundException
- ✅ Dispose pattern works
- ✅ ValidationException has field errors

**Expected Total: ~19 passed, 0 failed, ~7 skipped**

## Build Status

✅ **Build Successful** - 0 errors, 737 warnings (XML docs for stubs)

## Next Step

Run integration tests:
```bash
cd /root/saas/hookbase/hookbase-dotnet/tests/IntegrationTests/IntegrationTests
dotnet run
```

using Hookbase;
using Hookbase.Models.Common;
using Hookbase.Models.Sources;
using Hookbase.Models.Applications;
using Hookbase.Exceptions;

// Initialize the client
var apiKey = Environment.GetEnvironmentVariable("HOOKBASE_API_KEY") ?? "whr_your_api_key_here";
var client = new HookbaseClient(apiKey);

Console.WriteLine("Hookbase .NET SDK - Basic Usage Example\n");

try
{
    // ============================================
    // Working with Sources (Inbound Webhooks)
    // ============================================
    Console.WriteLine("=== Inbound Webhooks (Sources) ===\n");

    // List sources
    Console.WriteLine("Listing sources...");
    var sources = await client.Sources.ListAsync(page: 1, pageSize: 10);
    Console.WriteLine($"Found {sources.Total} sources ({sources.Data.Count} on this page)");

    foreach (var source in sources.Data)
    {
        Console.WriteLine($"  - {source.Name} ({source.Slug}) - Active: {source.IsActive}");
    }

    Console.WriteLine();

    // Create a new source
    Console.WriteLine("Creating a new source...");
    var newSource = await client.Sources.CreateAsync(new CreateSourceRequest
    {
        Name = "Example Stripe Webhooks",
        Slug = "stripe-example",
        Provider = SourceProvider.Stripe,
        RejectInvalidSignatures = true,
        Description = "Example webhook source for testing"
    });

    Console.WriteLine($"Created source: {newSource.Id}");
    Console.WriteLine($"Signing secret: {newSource.SigningSecret}");
    Console.WriteLine($"Ingest URL: {newSource.IngestUrl}");
    Console.WriteLine();

    // Source.Id is nullable on the model; the API always sets it on a created source.
    var sourceId = newSource.Id!;

    // Get the source we just created
    Console.WriteLine($"Fetching source {sourceId}...");
    var fetchedSource = await client.Sources.GetAsync(sourceId);
    Console.WriteLine($"Fetched: {fetchedSource.Name}");
    Console.WriteLine();

    // Update the source. UpdateAsync returns no body, so re-fetch to see the result.
    Console.WriteLine("Updating source...");
    await client.Sources.UpdateAsync(sourceId, new UpdateSourceRequest
    {
        Description = "Updated description",
        IsActive = true
    });
    var updatedSource = await client.Sources.GetAsync(sourceId);
    Console.WriteLine($"Updated description: {updatedSource.Description}");
    Console.WriteLine();

    // Auto-pagination example
    Console.WriteLine("Iterating through all sources...");
    var count = 0;
    await foreach (var source in client.Sources.ListAllAsync())
    {
        count++;
        Console.WriteLine($"  {count}. {source.Name}");

        if (count >= 5) // Limit output for demo
        {
            Console.WriteLine("  ... (showing first 5)");
            break;
        }
    }
    Console.WriteLine();

    // Clean up - delete the example source
    Console.WriteLine($"Deleting example source {sourceId}...");
    await client.Sources.DeleteAsync(sourceId);
    Console.WriteLine("Deleted successfully");
    Console.WriteLine();

    // ============================================
    // Working with Applications (Outbound Webhooks)
    // ============================================
    Console.WriteLine("=== Outbound Webhooks (Applications) ===\n");

    // List applications
    Console.WriteLine("Listing applications...");
    var apps = await client.Applications.ListAsync(limit: 10);
    Console.WriteLine($"Found {apps.Data.Count} applications");

    foreach (var app in apps.Data)
    {
        Console.WriteLine($"  - {app.Name} ({app.ExternalId})");
    }

    if (apps.HasMore)
    {
        Console.WriteLine($"  ... more available (next cursor: {apps.NextCursor})");
    }
    Console.WriteLine();

    // Create a new application
    Console.WriteLine("Creating a new application...");
    var newApp = await client.Applications.CreateAsync(new CreateApplicationRequest
    {
        Name = "Example Customer App",
        ExternalId = $"customer_{Guid.NewGuid():N}",
        Metadata = new Dictionary<string, object>
        {
            ["plan"] = "trial",
            ["region"] = "us-east-1",
            ["created_at"] = DateTime.UtcNow.ToString("O")
        }
    });

    Console.WriteLine($"Created application: {newApp.Id}");
    Console.WriteLine($"External ID: {newApp.ExternalId}");
    Console.WriteLine($"Metadata: {System.Text.Json.JsonSerializer.Serialize(newApp.Metadata)}");
    Console.WriteLine();

    // Get or create (idempotent operation)
    Console.WriteLine("Testing get-or-create with existing external ID...");
    var appOrCreate = await client.Applications.GetOrCreateAsync(new GetOrCreateApplicationRequest
    {
        ExternalId = newApp.ExternalId!,
        Name = "Example Customer App",
        Metadata = new Dictionary<string, object> { ["updated"] = "true" }
    });

    Console.WriteLine($"Got existing application: {appOrCreate.Id}");
    Console.WriteLine($"Same as created: {appOrCreate.Id == newApp.Id}");
    Console.WriteLine();

    // Clean up - delete the example application
    Console.WriteLine($"Deleting example application {newApp.Id}...");
    await client.Applications.DeleteAsync(newApp.Id);
    Console.WriteLine("Deleted successfully");
    Console.WriteLine();

    Console.WriteLine("=== Example completed successfully! ===");
}
catch (ValidationException ex)
{
    Console.WriteLine($"Validation error: {ex.Message}");
    if (ex.FieldErrors != null)
    {
        Console.WriteLine("Field errors:");
        foreach (var (field, errors) in ex.FieldErrors)
        {
            Console.WriteLine($"  {field}: {string.Join(", ", errors)}");
        }
    }
}
catch (NotFoundException ex)
{
    Console.WriteLine($"Not found: {ex.Message}");
    Console.WriteLine($"Request ID: {ex.RequestId}");
}
catch (RateLimitException ex)
{
    Console.WriteLine($"Rate limited: {ex.Message}");
    Console.WriteLine($"Retry after: {ex.RetryAfter} seconds");
}
catch (HookbaseApiException ex)
{
    Console.WriteLine($"API error ({ex.StatusCode}): {ex.Message}");
    Console.WriteLine($"Request ID: {ex.RequestId}");
}
catch (HookbaseException ex)
{
    Console.WriteLine($"SDK error: {ex.Message}");
}
finally
{
    // Dispose the client
    client.Dispose();
}

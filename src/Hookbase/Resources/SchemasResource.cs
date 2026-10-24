using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Schemas;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing JSON schemas for webhook validation.
/// </summary>
public class SchemasResource : BaseResource
{
    public SchemasResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List all schemas. API returns all schemas without pagination.
    /// </summary>
    public async Task<OffsetPage<Schema>> ListAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<ListSchemasResponse>(
            HttpMethod.Get,
            "/api/schemas",
            cancellationToken: cancellationToken
        );

        return new OffsetPage<Schema>
        {
            Data = response.Schemas,
            Total = response.Schemas.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Get a schema by ID.
    /// </summary>
    public async Task<Schema> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetSchemaResponse>(
            HttpMethod.Get,
            $"/api/schemas/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Schema;
    }

    /// <summary>
    /// Create a new schema.
    /// </summary>
    public async Task<Schema> CreateAsync(
        CreateSchemaRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<CreateSchemaResponse>(
            HttpMethod.Post,
            "/api/schemas",
            body: request,
            cancellationToken: cancellationToken
        );

        // The create response names the schema body json_schema (snake_case); map it.
        var created = response.Schema;
        return new Schema
        {
            Id = created.Id,
            Name = created.Name,
            Slug = created.Slug,
            Description = created.Description,
            JsonSchema = created.JsonSchema ?? created.JsonSchemaSnake,
        };
    }

    /// <summary>
    /// Update an existing schema.
    /// </summary>
    public async Task UpdateAsync(
        string id,
        UpdateSchemaRequest request,
        CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Put,
            $"/api/schemas/{Uri.EscapeDataString(id)}",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Delete a schema.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Delete,
            $"/api/schemas/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Validate a payload against a schema.
    /// </summary>
    public async Task<ValidateSchemaResult> ValidateAsync(
        ValidateSchemaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.SchemaId);

        // The route is POST /api/schemas/:schemaId/validate with { payload }; the old
        // /api/schemas/validate path matched no route.
        return await ApiClient.RequestAsync<ValidateSchemaResult>(
            HttpMethod.Post,
            $"/api/schemas/{Uri.EscapeDataString(request.SchemaId)}/validate",
            body: new { payload = request.Payload },
            cancellationToken: cancellationToken
        );
    }

    // Internal response types
    private record ListSchemasResponse
    {
        public required List<Schema> Schemas { get; init; }
    }

    private record CreateSchemaResponse
    {
        public required CreatedSchema Schema { get; init; }
    }

    private record CreatedSchema
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Slug { get; init; }
        public string? Description { get; init; }
        public object? JsonSchema { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("json_schema")]
        public object? JsonSchemaSnake { get; init; }
    }

    private record GetSchemaResponse
    {
        public required Schema Schema { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

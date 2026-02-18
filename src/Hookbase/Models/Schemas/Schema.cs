using System.Text.Json.Serialization;

namespace Hookbase.Models.Schemas;

/// <summary>
/// JSON schema for payload validation.
/// </summary>
public record Schema
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }

    [JsonPropertyName("jsonSchema")]
    public object? JsonSchema { get; init; }

    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Input for creating a new schema.
/// </summary>
public record CreateSchemaRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }

    [JsonPropertyName("jsonSchema")]
    public required object JsonSchema { get; init; }
}

/// <summary>
/// Input for updating an existing schema.
/// </summary>
public record UpdateSchemaRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }

    [JsonPropertyName("jsonSchema")]
    public object? JsonSchema { get; init; }
}

/// <summary>
/// Input for validating payload against schema.
/// </summary>
public record ValidateSchemaRequest
{
    public required string SchemaId { get; init; }
    public required Dictionary<string, object> Payload { get; init; }
}

/// <summary>
/// Result of schema validation.
/// </summary>
public record ValidateSchemaResult
{
    public bool Valid { get; init; }
    public List<string>? Errors { get; init; }
}

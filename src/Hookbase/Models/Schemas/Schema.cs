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
    public string? Slug { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// The JSON Schema. The list and get return it as JSON text (a string element); create returns
    /// the object. <see cref="JsonSchemaElement"/> gives the parsed form either way.
    /// </summary>
    [JsonPropertyName("jsonSchema")]
    public object? JsonSchema { get; init; }

    /// <summary><see cref="JsonSchema"/> parsed, whether the API sent it as text or as an object.</summary>
    [JsonIgnore]
    public System.Text.Json.JsonElement? JsonSchemaElement
    {
        get
        {
            try
            {
                return JsonSchema switch
                {
                    System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } text =>
                        System.Text.Json.JsonDocument.Parse(text.GetString()!).RootElement.Clone(),
                    System.Text.Json.JsonElement element => element,
                    string text => System.Text.Json.JsonDocument.Parse(text).RootElement.Clone(),
                    null => null,
                    var other => System.Text.Json.JsonSerializer.SerializeToElement(other),
                };
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
    }

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

    /// <summary>
    /// Validation errors. The API sends <c>{ path, message }</c> objects; this used to be
    /// <c>List&lt;string&gt;</c>, which threw on any failed validation.
    /// </summary>
    public List<SchemaValidationError>? Errors { get; init; }

    /// <summary>The payload the API validated, echoed back.</summary>
    public object? Payload { get; init; }
}

/// <summary>One schema validation error.</summary>
public record SchemaValidationError
{
    /// <summary>JSON path of the failing value (empty for the root).</summary>
    public string? Path { get; init; }
    public string? Message { get; init; }
}

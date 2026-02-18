using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Transforms;

/// <summary>
/// JSONata transform for webhook payloads.
/// </summary>
public record Transform
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public string? Code { get; init; }
    public string? TransformType { get; init; }
    public string? InputFormat { get; init; }
    public string? OutputFormat { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsActive { get; init; } = true;

    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Input for creating a new transform.
/// </summary>
public record CreateTransformRequest
{
    public required string Name { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public required string Code { get; init; }
    public string? TransformType { get; init; }
    public string? InputFormat { get; init; }
    public string? OutputFormat { get; init; }
}

/// <summary>
/// Input for updating an existing transform.
/// </summary>
public record UpdateTransformRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Code { get; init; }
    public string? TransformType { get; init; }
    public string? InputFormat { get; init; }
    public string? OutputFormat { get; init; }
}

/// <summary>
/// Input for testing a transform.
/// </summary>
public record TestTransformRequest
{
    public required string Code { get; init; }
    public required Dictionary<string, object> Payload { get; init; }
}

/// <summary>
/// Result of testing a transform.
/// </summary>
public record TestTransformResult
{
    public bool Success { get; init; }
    public object? Result { get; init; }
    public string? Error { get; init; }
}

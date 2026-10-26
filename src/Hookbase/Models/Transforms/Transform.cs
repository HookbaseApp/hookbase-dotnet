using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Transforms;

/// <summary>
/// Transform for webhook payloads (JSONata, JavaScript, TypeScript, Liquid, XSLT, JMESPath, JSON Logic or jq).
/// </summary>
public record Transform
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public string? Code { get; init; }
    /// <summary>Transform language: <c>jsonata</c> (API default), <c>javascript</c>, <c>typescript</c>, <c>liquid</c>, <c>xslt</c>, <c>jmespath</c>, <c>jsonlogic</c> or <c>jq</c>. <c>jq</c> needs transform isolation on the server; otherwise the API returns <c>TRANSFORM_ENGINE_UNAVAILABLE</c>.</summary>
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
    /// <summary>Transform language: <c>jsonata</c> (API default), <c>javascript</c>, <c>typescript</c>, <c>liquid</c>, <c>xslt</c>, <c>jmespath</c>, <c>jsonlogic</c> or <c>jq</c>. <c>jq</c> needs transform isolation on the server; otherwise the API returns <c>TRANSFORM_ENGINE_UNAVAILABLE</c>.</summary>
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
    /// <summary>Transform language: <c>jsonata</c> (API default), <c>javascript</c>, <c>typescript</c>, <c>liquid</c>, <c>xslt</c>, <c>jmespath</c>, <c>jsonlogic</c> or <c>jq</c>. <c>jq</c> needs transform isolation on the server; otherwise the API returns <c>TRANSFORM_ENGINE_UNAVAILABLE</c>.</summary>
    public string? TransformType { get; init; }
    public string? InputFormat { get; init; }
    public string? OutputFormat { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// Input for testing a transform.
/// </summary>
public record TestTransformRequest
{
    public required string Code { get; init; }
    public required Dictionary<string, object> Payload { get; init; }

    /// <summary>Transform language: <c>jsonata</c> (API default), <c>javascript</c>, <c>typescript</c>, <c>liquid</c>, <c>xslt</c>, <c>jmespath</c>, <c>jsonlogic</c> or <c>jq</c>. <c>jq</c> needs transform isolation on the server; otherwise the API returns <c>TRANSFORM_ENGINE_UNAVAILABLE</c>.</summary>
    public string? TransformType { get; init; }

    /// <summary>Input format; the API defaults to <c>json</c>.</summary>
    public string? InputFormat { get; init; }

    /// <summary>Output format; the API defaults to <c>json</c>.</summary>
    public string? OutputFormat { get; init; }
}

/// <summary>
/// Result of testing a transform.
/// </summary>
public record TestTransformResult
{
    public bool Success { get; init; }

    /// <summary>The transform's output: parsed JSON for JSON output, otherwise a string.</summary>
    public object? Result { get; init; }

    /// <summary>The output's content type.</summary>
    public string? ContentType { get; init; }

    public string? Error { get; init; }
}

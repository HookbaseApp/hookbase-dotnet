using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Filters;

/// <summary>
/// Reusable filter for webhook routing.
/// </summary>
public record Filter
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public object? Conditions { get; init; }
    public string? Logic { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsActive { get; init; } = true;

    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Input for creating a new filter.
/// </summary>
public record CreateFilterRequest
{
    public required string Name { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public required List<FilterConditionInput> Conditions { get; init; }
    public string? Logic { get; init; }
}

/// <summary>
/// A single filter condition for create/update requests.
/// </summary>
public record FilterConditionInput
{
    public required string Field { get; init; }
    public required string Operator { get; init; }
    public required object Value { get; init; }
}

/// <summary>
/// Input for updating an existing filter.
/// </summary>
public record UpdateFilterRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public List<FilterConditionInput>? Conditions { get; init; }
    public string? Logic { get; init; }
}

/// <summary>
/// Input for testing a filter.
/// </summary>
public record TestFilterRequest
{
    public required List<FilterConditionInput> Conditions { get; init; }
    public string? Logic { get; init; }
    public required Dictionary<string, object> Payload { get; init; }
}

/// <summary>
/// Result of testing a filter.
/// </summary>
public record TestFilterResult
{
    [Obsolete("The API does not return success; it is always false. A 4xx raises HookbaseApiException instead.")]
    public bool Success { get; init; }

    /// <summary>Whether the payload matched the conditions under <see cref="Logic"/> (the API's <c>matches</c>).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("matches")]
    public bool Matched { get; init; }

    /// <summary>The logic the API applied: <c>AND</c> or <c>OR</c>.</summary>
    public string? Logic { get; init; }

    /// <summary>Per-condition outcome.</summary>
    public List<FilterConditionResult>? Results { get; init; }

    [Obsolete("The API does not return error on a filter test; it is always null.")]
    public string? Error { get; init; }
}

/// <summary>How one condition evaluated against the test payload.</summary>
public record FilterConditionResult
{
    public string? Field { get; init; }
    public string? Operator { get; init; }
    public object? Value { get; init; }

    /// <summary>The value found at <see cref="Field"/> in the payload, or null if absent.</summary>
    public object? FieldValue { get; init; }

    public bool Passed { get; init; }
}

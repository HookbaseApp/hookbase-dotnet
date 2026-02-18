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
    public bool Success { get; init; }
    public bool Matched { get; init; }
    public string? Error { get; init; }
}

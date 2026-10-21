namespace Hookbase.Models.Common;

/// <summary>
/// How one item of a validate-only import (sources, destinations or routes) would be handled.
/// </summary>
public record ImportValidationResult
{
    /// <summary>The item's position in the list sent.</summary>
    public int Index { get; init; }

    public string? Name { get; init; }

    /// <summary>Sources and destinations only.</summary>
    public string? Slug { get; init; }

    /// <summary><c>valid</c>, <c>error</c>, <c>skip</c>, <c>rename</c> or <c>overwrite</c>.</summary>
    public string? Status { get; init; }

    public List<string>? Errors { get; init; }
    public List<string>? Warnings { get; init; }

    /// <summary>Sources/destinations under the <c>rename</c> strategy: the slug it gets.</summary>
    public string? ResolvedSlug { get; init; }

    /// <summary>Routes under the <c>rename</c> strategy: the name it gets.</summary>
    public string? ResolvedName { get; init; }
}

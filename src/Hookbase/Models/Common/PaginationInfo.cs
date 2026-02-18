namespace Hookbase.Models.Common;

/// <summary>
/// Pagination metadata for offset-based pagination.
/// </summary>
public record PaginationInfo
{
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>
/// Pagination metadata for cursor-based pagination.
/// </summary>
public record CursorPaginationInfo
{
    public bool HasMore { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
}

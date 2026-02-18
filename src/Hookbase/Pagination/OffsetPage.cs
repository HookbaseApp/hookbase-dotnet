namespace Hookbase.Pagination;

/// <summary>
/// Offset-based pagination response wrapper.
/// </summary>
public class OffsetPage<T>
{
    /// <summary>
    /// List of items in the current page.
    /// </summary>
    public required List<T> Data { get; init; }

    /// <summary>
    /// Total number of items across all pages.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Current page number (1-indexed).
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Whether there are more pages available.
    /// </summary>
    public bool HasMore => Page * PageSize < Total;

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}

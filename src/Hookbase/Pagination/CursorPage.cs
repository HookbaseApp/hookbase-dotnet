namespace Hookbase.Pagination;

/// <summary>
/// Cursor-based pagination response wrapper.
/// </summary>
public class CursorPage<T>
{
    /// <summary>
    /// List of items in the current page.
    /// </summary>
    public required List<T> Data { get; init; }

    /// <summary>
    /// Whether there are more pages available.
    /// </summary>
    public bool HasMore { get; init; }

    /// <summary>
    /// Cursor token for fetching the next page.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Cursor token for fetching the previous page.
    /// </summary>
    public string? PrevCursor { get; init; }
}

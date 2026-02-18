namespace Hookbase.Models.Common;

/// <summary>
/// Response envelope for single inbound resources (Sources, Destinations, Routes, etc.)
/// </summary>
internal record InboundResourceResponse<T>
{
    public required T Source { get; init; }
    public required T Destination { get; init; }
    public required T Route { get; init; }
    public required T Transform { get; init; }
    public required T Filter { get; init; }
    public required T Schema { get; init; }
}

/// <summary>
/// Response envelope for list inbound resources with offset pagination.
/// </summary>
internal record InboundListResponse<T>
{
    public required List<T> Sources { get; init; }
    public required List<T> Destinations { get; init; }
    public required List<T> Routes { get; init; }
    public required List<T> Events { get; init; }
    public required List<T> Deliveries { get; init; }
    public required List<T> Transforms { get; init; }
    public required List<T> Filters { get; init; }
    public required List<T> Schemas { get; init; }
    public required PaginationInfo Pagination { get; init; }
}

/// <summary>
/// Response envelope for single outbound resources (Applications, Endpoints, etc.)
/// </summary>
internal record OutboundResourceResponse<T>
{
    public required T Data { get; init; }
}

/// <summary>
/// Response envelope for list outbound resources with cursor pagination.
/// </summary>
internal record OutboundListResponse<T>
{
    public required List<T> Data { get; init; }
    public required CursorPaginationInfo Pagination { get; init; }
}

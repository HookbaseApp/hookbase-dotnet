using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Deliveries;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for viewing webhook delivery attempts.
/// </summary>
public class DeliveriesResource : BaseResource
{
    public DeliveriesResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List all deliveries with offset-based pagination.
    /// API uses limit/offset (not page/pageSize).
    /// </summary>
    public async Task<OffsetPage<Delivery>> ListAsync(
        int limit = 20,
        int offset = 0,
        string? eventId = null,
        string? destinationId = null,
        DeliveryStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["limit"] = limit.ToString(),
            ["offset"] = offset.ToString()
        };

        if (eventId != null) queryParams["eventId"] = eventId;
        if (destinationId != null) queryParams["destinationId"] = destinationId;
        if (status.HasValue) queryParams["status"] = status.Value.ToString().ToLower();

        var response = await ApiClient.RequestAsync<ListDeliveriesResponse>(
            HttpMethod.Get,
            "/api/deliveries",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        // The API caps limit at 100; page by the limit it actually applied, not the one requested.
        var pageSize = response.Limit > 0 ? response.Limit : limit;
        return new OffsetPage<Delivery>
        {
            Data = response.Deliveries,
            // The API returns no total. A full page means there may be more, so count one past it
            // (same as Events); a short page ends paging.
            Total = response.Offset + response.Deliveries.Count + (response.Deliveries.Count >= pageSize ? 1 : 0),
            Page = response.Offset / pageSize + 1,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Get detailed information about a delivery.
    /// </summary>
    public async Task<DeliveryDetail> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetDeliveryResponse>(
            HttpMethod.Get,
            $"/api/deliveries/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Delivery;
    }

    /// <summary>
    /// Replay a failed delivery.
    /// </summary>
    public async Task ReplayAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Post,
            $"/api/deliveries/{Uri.EscapeDataString(id)}/replay",
            cancellationToken: cancellationToken
        );
    }

    // Internal response types
    private record ListDeliveriesResponse
    {
        public required List<Delivery> Deliveries { get; init; }
        public int Limit { get; init; }
        public int Offset { get; init; }
    }

    private record GetDeliveryResponse
    {
        public required DeliveryDetail Delivery { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

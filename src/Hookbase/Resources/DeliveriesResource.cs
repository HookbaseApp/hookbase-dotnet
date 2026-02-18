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

        return new OffsetPage<Delivery>
        {
            Data = response.Deliveries,
            Total = response.Deliveries.Count, // API doesn't return total for deliveries
            Page = offset / limit + 1,
            PageSize = limit
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

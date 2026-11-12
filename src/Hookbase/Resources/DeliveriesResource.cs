using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Deliveries;
using Hookbase.Pagination;
using System.Text.Json;

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
        if (status.HasValue) queryParams["status"] = Hookbase.Json.WireNames.SnakeCase(status.Value);

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
    /// <remarks>
    /// To edit the payload, transform, headers or destination of the replay, or to read the new
    /// delivery's ID, use <see cref="ReplayAsync(string, ReplayDeliveryRequest, CancellationToken)"/>.
    /// </remarks>
    public async Task ReplayAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Post,
            $"/api/deliveries/{Uri.EscapeDataString(id)}/replay",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Replay a delivery with edits (<c>POST /api/deliveries/:id/replay</c>): a replaced or patched
    /// payload, another destination, a transform or header override. The replay is a new delivery;
    /// the original keeps its status.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Both <see cref="ReplayDeliveryRequest.ModifiedPayload"/> and
    /// <see cref="ReplayDeliveryRequest.PayloadPatch"/> are set, the patch is not a JSON object, or
    /// <see cref="ReplayDeliveryRequest.PersistTransform"/> is true without a
    /// <see cref="ReplayDeliveryRequest.TransformOverride"/>. The API refuses all three; nothing is sent.
    /// </exception>
    public async Task<ReplayDeliveryResult> ReplayAsync(
        string id,
        ReplayDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = ReplayBody(request);
        return await ApiClient.RequestAsync<ReplayDeliveryResult>(
            HttpMethod.Post,
            $"/api/deliveries/{Uri.EscapeDataString(id)}/replay",
            body: body,
            cancellationToken: cancellationToken
        );
    }

    private static Dictionary<string, object> ReplayBody(ReplayDeliveryRequest request)
    {
        // A default JsonElement (ValueKind Undefined) has nothing to send; treat it as unset.
        var modified = request.ModifiedPayload is { ValueKind: not JsonValueKind.Undefined } m ? m : (JsonElement?)null;
        var patch = request.PayloadPatch is { ValueKind: not JsonValueKind.Undefined } p ? p : (JsonElement?)null;

        if (modified != null && patch != null)
        {
            throw new ArgumentException(
                "Set either ModifiedPayload or PayloadPatch, not both.", nameof(request));
        }
        if (patch is { ValueKind: not JsonValueKind.Object })
        {
            throw new ArgumentException(
                "PayloadPatch must be a JSON object (an RFC 7386 merge patch).", nameof(request));
        }
        if (request.PersistTransform == true && request.TransformOverride == null)
        {
            throw new ArgumentException(
                "PersistTransform needs a TransformOverride to save.", nameof(request));
        }

        var body = new Dictionary<string, object>();
        if (modified != null) body["modifiedPayload"] = modified.Value;
        if (patch != null) body["payloadPatch"] = patch.Value;
        if (request.DestinationOverride != null) body["destinationOverride"] = request.DestinationOverride;
        if (request.TransformOverride != null) body["transformOverride"] = request.TransformOverride;
        if (request.HeadersOverride != null) body["headersOverride"] = request.HeadersOverride;
        if (request.PersistTransform != null) body["persistTransform"] = request.PersistTransform.Value;
        return body;
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

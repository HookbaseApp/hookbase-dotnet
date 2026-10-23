using Hookbase.Http;
using Hookbase.Models.Analytics;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for analytics dashboard data.
/// </summary>
public class AnalyticsResource : BaseResource
{
    public AnalyticsResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// Get analytics dashboard data.
    /// </summary>
    public async Task<DashboardData> DashboardAsync(
        string? range = null,
        string? startDate = null,
        string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        return await DashboardAsync(
            new DashboardOptions { Range = range, StartDate = startDate, EndDate = endDate },
            cancellationToken);
    }

    /// <summary>
    /// Get analytics dashboard data, with time zone and source/destination scope.
    /// </summary>
    public async Task<DashboardData> DashboardAsync(
        DashboardOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var queryParams = new Dictionary<string, string>();
        if (options.Range != null) queryParams["range"] = options.Range;
        if (options.StartDate != null) queryParams["startDate"] = options.StartDate;
        if (options.EndDate != null) queryParams["endDate"] = options.EndDate;
        if (options.TimeZone != null) queryParams["tz"] = options.TimeZone;
        if (options.SourceId != null) queryParams["sourceId"] = options.SourceId;
        if (options.DestinationId != null) queryParams["destinationId"] = options.DestinationId;

        return await ApiClient.RequestAsync<DashboardData>(
            HttpMethod.Get,
            "/api/analytics/dashboard",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );
    }
}

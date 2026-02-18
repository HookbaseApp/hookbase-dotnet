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
        var queryParams = new Dictionary<string, string>();
        if (range != null) queryParams["range"] = range;
        if (startDate != null) queryParams["startDate"] = startDate;
        if (endDate != null) queryParams["endDate"] = endDate;

        return await ApiClient.RequestAsync<DashboardData>(
            HttpMethod.Get,
            "/api/analytics/dashboard",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );
    }
}

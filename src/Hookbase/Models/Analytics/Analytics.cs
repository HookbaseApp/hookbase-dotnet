using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Analytics;

/// <summary>
/// Analytics dashboard data (<c>GET /api/analytics/dashboard</c>).
/// </summary>
public record DashboardData
{
    /// <summary>Totals for the range.</summary>
    public DashboardOverview? Overview { get; init; }

    /// <summary>One point per interval: <c>period</c>, <c>events</c>, <c>successful</c>, <c>failed</c>.</summary>
    public List<Dictionary<string, object>>? Timeline { get; init; }

    /// <summary>Latency percentiles and summary.</summary>
    public JsonElement? Latency { get; init; }
    public JsonElement? LatencyDistribution { get; init; }
    public JsonElement? LatencyTrends { get; init; }
    public JsonElement? Errors { get; init; }
    public JsonElement? Retries { get; init; }
    public JsonElement? Sources { get; init; }
    public JsonElement? Destinations { get; init; }
    public JsonElement? Routes { get; init; }
    public JsonElement? PayloadSizes { get; init; }

    /// <summary>The range the API applied, e.g. <c>7d</c>.</summary>
    public string? Range { get; init; }

    /// <summary>The timeline bucket size, e.g. <c>hour</c> or <c>day</c>.</summary>
    public string? Interval { get; init; }

    // The flat fields below never matched anything the API returns (they were always 0); they now
    // read from Overview.

    /// <summary><see cref="DashboardOverview.TotalEvents"/>.</summary>
    [JsonIgnore]
    public int EventsReceived => (int)(Overview?.TotalEvents ?? 0);

    /// <summary><see cref="DashboardOverview.SuccessfulDeliveries"/>.</summary>
    [JsonIgnore]
    public int DeliveriesCompleted => (int)(Overview?.SuccessfulDeliveries ?? 0);

    /// <summary>Successful deliveries as a percentage (0-100) of successful plus failed; 0 when there were none.</summary>
    [JsonIgnore]
    public double DeliverySuccessRate
    {
        get
        {
            var ok = Overview?.SuccessfulDeliveries ?? 0;
            var total = ok + (Overview?.FailedDeliveries ?? 0);
            return total == 0 ? 0 : ok * 100.0 / total;
        }
    }

    /// <summary><see cref="DashboardOverview.SourceCount"/>.</summary>
    [JsonIgnore]
    public int ActiveSources => (int)(Overview?.SourceCount ?? 0);

    /// <summary><see cref="DashboardOverview.DestinationCount"/>.</summary>
    [JsonIgnore]
    public int ActiveDestinations => (int)(Overview?.DestinationCount ?? 0);

    /// <summary>The number of routes with traffic in the range (the length of <see cref="Routes"/>).</summary>
    [JsonIgnore]
    public int ActiveRoutes => Routes is { ValueKind: JsonValueKind.Array } routes ? routes.GetArrayLength() : 0;
}

/// <summary>Dashboard totals. The API names these in snake_case.</summary>
public record DashboardOverview
{
    [JsonPropertyName("total_events")]
    public long TotalEvents { get; init; }

    [JsonPropertyName("successful_deliveries")]
    public long SuccessfulDeliveries { get; init; }

    [JsonPropertyName("failed_deliveries")]
    public long FailedDeliveries { get; init; }

    /// <summary>Average delivery latency in milliseconds, or null with no deliveries.</summary>
    [JsonPropertyName("avg_latency")]
    public double? AvgLatency { get; init; }

    [JsonPropertyName("source_count")]
    public long? SourceCount { get; init; }

    [JsonPropertyName("destination_count")]
    public long? DestinationCount { get; init; }
}

/// <summary>Filters for <c>Analytics.DashboardAsync</c>.</summary>
public record DashboardOptions
{
    /// <summary><c>1h</c>, <c>24h</c>, <c>7d</c> (API default), <c>30d</c> and so on.</summary>
    public string? Range { get; init; }
    public string? StartDate { get; init; }
    public string? EndDate { get; init; }

    /// <summary>IANA time zone for the timeline buckets, e.g. <c>America/Chicago</c> (sent as <c>tz</c>).</summary>
    public string? TimeZone { get; init; }

    /// <summary>Limit the figures to one source.</summary>
    public string? SourceId { get; init; }

    /// <summary>Limit the figures to one destination.</summary>
    public string? DestinationId { get; init; }
}

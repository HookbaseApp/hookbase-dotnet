namespace Hookbase.Models.Analytics;

/// <summary>
/// Analytics dashboard data.
/// </summary>
public record DashboardData
{
    public int EventsReceived { get; init; }
    public int DeliveriesCompleted { get; init; }
    public double DeliverySuccessRate { get; init; }
    public int ActiveSources { get; init; }
    public int ActiveDestinations { get; init; }
    public int ActiveRoutes { get; init; }
    public List<Dictionary<string, object>>? Timeline { get; init; }
}

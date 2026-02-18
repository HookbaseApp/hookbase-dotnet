using Hookbase.Http;
using Hookbase.Resources;

namespace Hookbase;

/// <summary>
/// Main client for interacting with the Hookbase API.
/// </summary>
public class HookbaseClient : IDisposable
{
    private readonly IApiClient _apiClient;
    private readonly HttpClient? _ownedHttpClient;

    /// <summary>
    /// Webhook sources - inbound webhook endpoints.
    /// </summary>
    public SourcesResource Sources { get; }

    /// <summary>
    /// Delivery destinations - where webhooks are forwarded.
    /// </summary>
    public DestinationsResource Destinations { get; }

    /// <summary>
    /// Routing rules for webhook processing.
    /// </summary>
    public RoutesResource Routes { get; }

    /// <summary>
    /// Inbound webhook events.
    /// </summary>
    public EventsResource Events { get; }

    /// <summary>
    /// Webhook delivery attempts and results.
    /// </summary>
    public DeliveriesResource Deliveries { get; }

    /// <summary>
    /// JSONata transforms for webhook payloads.
    /// </summary>
    public TransformsResource Transforms { get; }

    /// <summary>
    /// Filters for webhook routing.
    /// </summary>
    public FiltersResource Filters { get; }

    /// <summary>
    /// JSON schemas for validation.
    /// </summary>
    public SchemasResource Schemas { get; }

    /// <summary>
    /// Customer webhook applications.
    /// </summary>
    public ApplicationsResource Applications { get; }

    /// <summary>
    /// Customer webhook endpoints.
    /// </summary>
    public EndpointsResource Endpoints { get; }

    /// <summary>
    /// Event types for outbound webhooks.
    /// </summary>
    public EventTypesResource EventTypes { get; }

    /// <summary>
    /// Webhook subscriptions.
    /// </summary>
    public SubscriptionsResource Subscriptions { get; }

    /// <summary>
    /// Outbound webhook messages.
    /// </summary>
    public MessagesResource Messages { get; }

    /// <summary>
    /// Portal access tokens for customer self-service.
    /// </summary>
    public PortalTokensResource PortalTokens { get; }

    /// <summary>
    /// Dead letter queue for failed deliveries.
    /// </summary>
    public DlqResource Dlq { get; }

    /// <summary>
    /// Analytics dashboard data.
    /// </summary>
    public AnalyticsResource Analytics { get; }

    /// <summary>
    /// Tunnels for local development.
    /// </summary>
    public TunnelsResource Tunnels { get; }

    /// <summary>
    /// Scheduled cron jobs.
    /// </summary>
    public CronJobsResource CronJobs { get; }

    /// <summary>
    /// API keys for authentication.
    /// </summary>
    public ApiKeysResource ApiKeys { get; }

    /// <summary>
    /// Creates a new Hookbase client with the specified options.
    /// </summary>
    public HookbaseClient(HookbaseClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("API key is required", nameof(options));
        }

        HttpClient httpClient;
        if (options.HttpClient != null)
        {
            httpClient = options.HttpClient;
            _ownedHttpClient = null;
        }
        else
        {
            httpClient = new HttpClient();
            _ownedHttpClient = httpClient;
        }

        _apiClient = new ApiClient(
            options.ApiKey,
            options.BaseUrl,
            options.Timeout,
            options.MaxRetries,
            httpClient
        );

        // Initialize resources
        Sources = new SourcesResource(_apiClient);
        Destinations = new DestinationsResource(_apiClient);
        Routes = new RoutesResource(_apiClient);
        Events = new EventsResource(_apiClient);
        Deliveries = new DeliveriesResource(_apiClient);
        Transforms = new TransformsResource(_apiClient);
        Filters = new FiltersResource(_apiClient);
        Schemas = new SchemasResource(_apiClient);
        Applications = new ApplicationsResource(_apiClient);
        Endpoints = new EndpointsResource(_apiClient);
        EventTypes = new EventTypesResource(_apiClient);
        Subscriptions = new SubscriptionsResource(_apiClient);
        Messages = new MessagesResource(_apiClient);
        PortalTokens = new PortalTokensResource(_apiClient);
        Dlq = new DlqResource(_apiClient);
        Analytics = new AnalyticsResource(_apiClient);
        Tunnels = new TunnelsResource(_apiClient);
        CronJobs = new CronJobsResource(_apiClient);
        ApiKeys = new ApiKeysResource(_apiClient);
    }

    /// <summary>
    /// Creates a new Hookbase client with an API key.
    /// </summary>
    /// <param name="apiKey">API key for authentication</param>
    public HookbaseClient(string apiKey) : this(new HookbaseClientOptions { ApiKey = apiKey })
    {
    }

    public void Dispose()
    {
        if (_apiClient is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _ownedHttpClient?.Dispose();
    }
}

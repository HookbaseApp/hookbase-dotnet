using Hookbase.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Models.CronJobs;

/// <summary>
/// Scheduled cron job for periodic webhook delivery.
/// </summary>
public record CronJob
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
    public object? Headers { get; init; }
    public string? Payload { get; init; }
    public string? CronExpression { get; init; }
    public string? Timezone { get; init; }
    public int TimeoutMs { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsActive { get; init; } = true;

    [JsonConverter(typeof(BooleanConverter))]
    public bool UseStaticIp { get; init; } = true;

    public string? GroupId { get; init; }
    public string? LastRunAt { get; init; }
    public string? NextRunAt { get; init; }
    public int ConsecutiveFailures { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool NotifyOnFailure { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool NotifyOnSuccess { get; init; }

    public string? NotifyEmails { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Input for creating a new cron job.
/// </summary>
public record CreateCronJobRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Url { get; init; }
    public string? Method { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? Payload { get; init; }
    public required string CronExpression { get; init; }
    public string? Timezone { get; init; }
    public int? TimeoutMs { get; init; }
    public bool? IsActive { get; init; }
    public bool? UseStaticIp { get; init; }
    public string? GroupId { get; init; }
    public bool? NotifyOnFailure { get; init; }
    public bool? NotifyOnSuccess { get; init; }
    public string? NotifyEmails { get; init; }
}

/// <summary>
/// Input for updating a cron job.
/// </summary>
public record UpdateCronJobRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? Payload { get; init; }
    public string? CronExpression { get; init; }
    public string? Timezone { get; init; }
    public int? TimeoutMs { get; init; }
    public bool? IsActive { get; init; }
    public bool? UseStaticIp { get; init; }
    public string? GroupId { get; init; }
    public bool? NotifyOnFailure { get; init; }
    public bool? NotifyOnSuccess { get; init; }
    public string? NotifyEmails { get; init; }
}

/// <summary>
/// Cron job group for organization.
/// </summary>
public record CronGroup
{
    public string? Id { get; init; }
    public string? OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? CreatedAt { get; init; }
}

/// <summary>
/// Input for creating a cron group.
/// </summary>
public record CreateCronGroupRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Result of manually triggering a cron job.
/// </summary>
public record CronJobExecution
{
    public string? Id { get; init; }
    public string? CronJobId { get; init; }
    public string? Status { get; init; }
    public int? ResponseStatus { get; init; }
    public string? ResponseBody { get; init; }

    private readonly string? _errorMessage;

    /// <summary>
    /// Why the run failed (timeout, connection error), or null. The execution list sends it as
    /// <c>errorMessage</c>, a failed <c>TriggerAsync</c> as <c>error</c>; both land here.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage ?? Error;
        init => _errorMessage = value;
    }

    /// <summary>The <c>error</c> field a failed manual trigger returns. See <see cref="ErrorMessage"/>.</summary>
    public string? Error { get; init; }

    public int LatencyMs { get; init; }
    public string? StartedAt { get; init; }
    public string? CompletedAt { get; init; }
}

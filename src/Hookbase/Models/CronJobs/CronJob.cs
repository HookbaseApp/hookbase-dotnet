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
    public string? Body { get; init; }
    public string? CronExpression { get; init; }
    public string? Timezone { get; init; }

    [JsonConverter(typeof(BooleanConverter))]
    public bool IsActive { get; init; } = true;

    public string? GroupId { get; init; }
    public string? LastRunAt { get; init; }
    public string? NextRunAt { get; init; }
    public string? LastStatus { get; init; }
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
    public string? Body { get; init; }
    public required string CronExpression { get; init; }
    public string? Timezone { get; init; }
    public bool? IsActive { get; init; }
    public string? GroupId { get; init; }
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
    public string? Body { get; init; }
    public string? CronExpression { get; init; }
    public string? Timezone { get; init; }
    public bool? IsActive { get; init; }
    public string? GroupId { get; init; }
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

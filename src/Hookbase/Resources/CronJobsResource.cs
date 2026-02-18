using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.CronJobs;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing scheduled cron jobs.
/// </summary>
public class CronJobsResource : BaseResource
{
    public CronJobsResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List cron jobs with offset-based pagination.
    /// </summary>
    public async Task<OffsetPage<CronJob>> ListAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        var response = await ApiClient.RequestAsync<ListCronJobsResponse>(
            HttpMethod.Get,
            "/api/cron",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new OffsetPage<CronJob>
        {
            Data = response.CronJobs,
            Total = response.Pagination?.Total ?? response.CronJobs.Count,
            Page = response.Pagination?.Page ?? page,
            PageSize = response.Pagination?.PageSize ?? pageSize
        };
    }

    /// <summary>
    /// Get a cron job by ID.
    /// </summary>
    public async Task<CronJob> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetCronJobResponse>(
            HttpMethod.Get,
            $"/api/cron/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.CronJob;
    }

    /// <summary>
    /// Create a new cron job.
    /// </summary>
    public async Task<CronJob> CreateAsync(
        CreateCronJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetCronJobResponse>(
            HttpMethod.Post,
            "/api/cron",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.CronJob;
    }

    /// <summary>
    /// Update a cron job.
    /// </summary>
    public async Task UpdateAsync(
        string id,
        UpdateCronJobRequest request,
        CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Patch,
            $"/api/cron/{Uri.EscapeDataString(id)}",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Delete a cron job.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Delete,
            $"/api/cron/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// List cron groups.
    /// </summary>
    public async Task<List<CronGroup>> ListGroupsAsync(CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<ListCronGroupsResponse>(
            HttpMethod.Get,
            "/api/cron-groups",
            cancellationToken: cancellationToken
        );

        return response.Groups;
    }

    /// <summary>
    /// Create a cron group.
    /// </summary>
    public async Task<CronGroup> CreateGroupAsync(
        CreateCronGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetCronGroupResponse>(
            HttpMethod.Post,
            "/api/cron-groups",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Group;
    }

    // Internal response types
    private record ListCronJobsResponse
    {
        public required List<CronJob> CronJobs { get; init; }
        public PaginationInfo? Pagination { get; init; }
    }

    private record GetCronJobResponse
    {
        public required CronJob CronJob { get; init; }
    }

    private record ListCronGroupsResponse
    {
        public required List<CronGroup> Groups { get; init; }
    }

    private record GetCronGroupResponse
    {
        public required CronGroup Group { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

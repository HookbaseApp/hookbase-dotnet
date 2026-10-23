using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.Transforms;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing webhook payload transforms.
/// </summary>
public class TransformsResource : BaseResource
{
    public TransformsResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List all transforms with offset-based pagination.
    /// </summary>
    public async Task<OffsetPage<Transform>> ListAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        var response = await ApiClient.RequestAsync<ListTransformsResponse>(
            HttpMethod.Get,
            "/api/transforms",
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return new OffsetPage<Transform>
        {
            Data = response.Transforms,
            Total = response.Pagination.Total,
            Page = response.Pagination.Page,
            PageSize = response.Pagination.PageSize
        };
    }

    /// <summary>
    /// Get a transform by ID.
    /// </summary>
    public async Task<Transform> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetTransformResponse>(
            HttpMethod.Get,
            $"/api/transforms/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );

        return response.Transform;
    }

    /// <summary>
    /// Create a new transform.
    /// </summary>
    public async Task<Transform> CreateAsync(
        CreateTransformRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<CreateTransformResponse>(
            HttpMethod.Post,
            "/api/transforms",
            body: request,
            cancellationToken: cancellationToken
        );

        // The create response names three fields in snake_case (transforms.ts); map them.
        var created = response.Transform;
        return new Transform
        {
            Id = created.Id,
            Name = created.Name,
            Slug = created.Slug,
            Description = created.Description,
            Code = created.Code,
            TransformType = created.TransformType ?? created.TransformTypeSnake,
            InputFormat = created.InputFormat ?? created.InputFormatSnake,
            OutputFormat = created.OutputFormat ?? created.OutputFormatSnake,
            IsActive = true,
        };
    }

    /// <summary>
    /// Update an existing transform and return it as it now is.
    /// </summary>
    /// <remarks>
    /// The API answers the PATCH with <c>{ success: true }</c> only, so this reads the transform
    /// back afterwards (two requests). It used to parse the PATCH response as a transform, which
    /// threw after the update had already been applied.
    /// </remarks>
    public async Task<Transform> UpdateAsync(
        string id,
        UpdateTransformRequest request,
        CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Patch,
            $"/api/transforms/{Uri.EscapeDataString(id)}",
            body: request,
            cancellationToken: cancellationToken
        );

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Delete a transform.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Delete,
            $"/api/transforms/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Test transform execution against sample payload.
    /// </summary>
    public async Task<TestTransformResult> TestAsync(
        TestTransformRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ApiClient.RequestAsync<TestTransformResult>(
            HttpMethod.Post,
            "/api/transforms/test",
            body: request,
            cancellationToken: cancellationToken
        );
    }

    // Internal response types
    private record ListTransformsResponse
    {
        public required List<Transform> Transforms { get; init; }
        public required PaginationInfo Pagination { get; init; }
    }

    private record GetTransformResponse
    {
        public required Transform Transform { get; init; }
    }

    private record CreateTransformResponse
    {
        public required CreatedTransform Transform { get; init; }
    }

    private record CreatedTransform
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Slug { get; init; }
        public string? Description { get; init; }
        public string? Code { get; init; }
        public string? TransformType { get; init; }
        public string? InputFormat { get; init; }
        public string? OutputFormat { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("transform_type")]
        public string? TransformTypeSnake { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("input_format")]
        public string? InputFormatSnake { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("output_format")]
        public string? OutputFormatSnake { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

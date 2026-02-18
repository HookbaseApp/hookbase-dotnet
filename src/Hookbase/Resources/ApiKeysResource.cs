using Hookbase.Http;
using Hookbase.Models.ApiKeys;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing API keys.
/// </summary>
public class ApiKeysResource : BaseResource
{
    public ApiKeysResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// List all API keys.
    /// </summary>
    public async Task<List<ApiKey>> ListAsync(CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<ListApiKeysResponse>(
            HttpMethod.Get,
            "/api/api-keys",
            cancellationToken: cancellationToken
        );

        return response.ApiKeys;
    }

    // Internal response types
    private record ListApiKeysResponse
    {
        public required List<ApiKey> ApiKeys { get; init; }
    }
}

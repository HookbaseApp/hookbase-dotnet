using Hookbase.Http;
using Hookbase.Models.Common;
using Hookbase.Models.PortalTokens;
using Hookbase.Pagination;

namespace Hookbase.Resources;

/// <summary>
/// Resource client for managing customer portal access tokens.
/// </summary>
public class PortalTokensResource : BaseResource
{
    public PortalTokensResource(IApiClient apiClient) : base(apiClient)
    {
    }

    /// <summary>
    /// Create a portal token for customer access.
    /// </summary>
    public async Task<PortalToken> CreateAsync(
        string applicationId,
        CreatePortalTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<GetPortalTokenResponse>(
            HttpMethod.Post,
            $"/api/portal/webhook-applications/{Uri.EscapeDataString(applicationId)}/tokens",
            body: request,
            cancellationToken: cancellationToken
        );

        return response.Data;
    }

    /// <summary>
    /// List portal tokens for an application.
    /// API returns all tokens without pagination.
    /// </summary>
    public async Task<CursorPage<PortalToken>> ListAsync(
        string applicationId,
        int limit = 50,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var response = await ApiClient.RequestAsync<ListPortalTokensResponse>(
            HttpMethod.Get,
            $"/api/portal/webhook-applications/{Uri.EscapeDataString(applicationId)}/tokens",
            cancellationToken: cancellationToken
        );

        return new CursorPage<PortalToken>
        {
            Data = response.Data,
            HasMore = false,
            NextCursor = null
        };
    }

    /// <summary>
    /// Revoke a portal token.
    /// </summary>
    public async Task RevokeAsync(
        string applicationId,
        string id,
        CancellationToken cancellationToken = default)
    {
        await ApiClient.RequestAsync<SuccessResponse>(
            HttpMethod.Delete,
            $"/api/portal/tokens/{Uri.EscapeDataString(id)}",
            cancellationToken: cancellationToken
        );
    }

    // Internal response types
    private record ListPortalTokensResponse
    {
        public required List<PortalToken> Data { get; init; }
    }

    private record GetPortalTokenResponse
    {
        public required PortalToken Data { get; init; }
    }

    private record SuccessResponse
    {
        public bool Success { get; init; }
    }
}

using System.Reflection;
using System.Text.Json;
using Hookbase.Http;

namespace Hookbase.Resources;

/// <summary>
/// Base class for all resource clients.
/// </summary>
public abstract class BaseResource
{
    protected readonly IApiClient ApiClient;

    protected BaseResource(IApiClient apiClient)
    {
        ApiClient = apiClient;
    }

    /// <summary>
    /// Builds query parameters from an object's properties.
    /// </summary>
    protected Dictionary<string, string> BuildQueryParams(object? parameters)
    {
        if (parameters == null)
        {
            return new Dictionary<string, string>();
        }

        var queryParams = new Dictionary<string, string>();
        var properties = parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var value = property.GetValue(parameters);
            if (value == null) continue;

            var key = ToCamelCase(property.Name);
            queryParams[key] = value.ToString()!;
        }

        return queryParams;
    }

    /// <summary>
    /// Converts property name to camelCase for query parameters.
    /// </summary>
    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
        {
            return str;
        }

        return char.ToLowerInvariant(str[0]) + str.Substring(1);
    }

    /// <summary>
    /// Generates a cryptographically secure random idempotency key.
    /// </summary>
    protected static string GenerateIdempotencyKey()
    {
        return Guid.NewGuid().ToString("N");
    }
}

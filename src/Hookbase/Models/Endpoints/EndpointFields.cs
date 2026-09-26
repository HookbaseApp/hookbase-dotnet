using Hookbase.Json;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Hookbase.Models.Endpoints;

/// <summary>
/// A single custom request header that Hookbase sends with every delivery to an endpoint.
/// </summary>
/// <remarks>
/// The API only accepts <c>headers</c> as an array of <c>{"name": "...", "value": "..."}</c>
/// objects (at most 10 entries). This type exists so that shape cannot be got wrong.
/// </remarks>
public record EndpointHeader(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value)
{
    /// <summary>Allows <c>("X-Api-Key", "secret")</c> wherever an <see cref="EndpointHeader"/> is expected.</summary>
    public static implicit operator EndpointHeader((string Name, string Value) header)
        => new(header.Name, header.Value);

    /// <summary>Allows a <see cref="KeyValuePair{TKey,TValue}"/> wherever an <see cref="EndpointHeader"/> is expected.</summary>
    public static implicit operator EndpointHeader(KeyValuePair<string, string> header)
        => new(header.Key, header.Value);
}

/// <summary>
/// The list of custom headers for an endpoint. Serializes as
/// <c>[{"name": "...", "value": "..."}]</c>, the only shape the API accepts.
/// </summary>
/// <remarks>
/// Earlier releases typed this collection as <c>List&lt;object&gt;</c>, which let callers send
/// any shape at all. Assigning a <c>List&lt;object&gt;</c> still compiles (see the obsolete
/// conversion below) and is normalized on the way to the wire, but new code should use
/// <see cref="EndpointHeader"/> elements.
/// </remarks>
[JsonConverter(typeof(EndpointHeaderListConverter))]
public class EndpointHeaderList : List<EndpointHeader>
{
    public EndpointHeaderList()
    {
    }

    public EndpointHeaderList(IEnumerable<EndpointHeader> headers) : base(headers)
    {
    }

    /// <summary>Adds a header without constructing an <see cref="EndpointHeader"/> explicitly.</summary>
    public void Add(string name, string value) => Add(new EndpointHeader(name, value));

    /// <summary>
    /// Accepts the legacy untyped header list so that existing code keeps compiling.
    /// Each element is normalized to a <c>{name, value}</c> pair.
    /// </summary>
    [Obsolete("Use EndpointHeader elements (or the (name, value) tuple conversion) instead of List<object>. " +
              "The API only accepts [{\"name\":...,\"value\":...}] and untyped elements are normalized on a best-effort basis.")]
    public static implicit operator EndpointHeaderList(List<object> headers) => FromLegacy(headers);

    /// <summary>
    /// Normalizes an untyped header collection into <see cref="EndpointHeader"/> values.
    /// Supports <see cref="EndpointHeader"/>, <c>(string, string)</c> tuples,
    /// <see cref="KeyValuePair{TKey,TValue}"/>, dictionaries with <c>name</c>/<c>value</c> keys,
    /// and any object exposing <c>Name</c>/<c>Value</c> (or <c>name</c>/<c>value</c>) members.
    /// </summary>
    public static EndpointHeaderList FromLegacy(IEnumerable<object?>? headers)
    {
        var result = new EndpointHeaderList();
        if (headers == null)
        {
            return result;
        }

        foreach (var entry in headers)
        {
            if (entry == null)
            {
                continue;
            }

            result.Add(Normalize(entry));
        }

        return result;
    }

    private static EndpointHeader Normalize(object entry)
    {
        switch (entry)
        {
            case EndpointHeader header:
                return header;
            case ValueTuple<string, string> tuple:
                return new EndpointHeader(tuple.Item1, tuple.Item2);
            case KeyValuePair<string, string> pair:
                return new EndpointHeader(pair.Key, pair.Value);
        }

        var name = ReadMember(entry, "name");
        var value = ReadMember(entry, "value");

        if (name == null || value == null)
        {
            throw new ArgumentException(
                $"Cannot convert '{entry.GetType().Name}' to an endpoint header. " +
                "Provide an EndpointHeader, a (name, value) tuple, or an object with 'name' and 'value' members.",
                nameof(entry));
        }

        return new EndpointHeader(name, value);
    }

    private static string? ReadMember(object entry, string member)
    {
        if (entry is IDictionary<string, string> stringMap)
        {
            return stringMap.TryGetValue(member, out var fromStringMap) ? fromStringMap : null;
        }

        if (entry is IDictionary<string, object> objectMap)
        {
            return objectMap.TryGetValue(member, out var fromObjectMap) ? fromObjectMap?.ToString() : null;
        }

        var property = entry.GetType().GetProperty(
            member,
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.IgnoreCase);

        return property?.GetValue(entry)?.ToString();
    }
}

/// <summary>
/// One entry of <c>successStatusCodes</c>: either an exact status code (<c>200</c>)
/// or a wildcard pattern string (<c>"2xx"</c>).
/// </summary>
/// <remarks>
/// The API accepts a mixed array whose elements are integers or strings, so a single
/// CLR element type has to be able to serialize as both. Implicit conversions mean callers
/// can just write <c>new() { 200, "2xx" }</c>.
/// </remarks>
[JsonConverter(typeof(SuccessStatusCodeConverter))]
public readonly struct SuccessStatusCode : IEquatable<SuccessStatusCode>
{
    private SuccessStatusCode(int? status, string? pattern)
    {
        Status = status;
        Pattern = pattern;
    }

    /// <summary>The exact status code, when this entry is numeric.</summary>
    public int? Status { get; }

    /// <summary>The wildcard pattern (for example <c>"2xx"</c>), when this entry is a string.</summary>
    public string? Pattern { get; }

    /// <summary>True when this entry serializes as a JSON string rather than a number.</summary>
    public bool IsPattern => Pattern != null;

    /// <summary>Creates a numeric entry, serialized as a JSON number.</summary>
    public static SuccessStatusCode FromStatus(int status) => new(status, null);

    /// <summary>Creates a pattern entry, serialized as a JSON string.</summary>
    public static SuccessStatusCode FromPattern(string pattern)
        => new(null, pattern ?? throw new ArgumentNullException(nameof(pattern)));

    public static implicit operator SuccessStatusCode(int status) => FromStatus(status);

    public static implicit operator SuccessStatusCode(string pattern) => FromPattern(pattern);

    public bool Equals(SuccessStatusCode other)
        => Status == other.Status && string.Equals(Pattern, other.Pattern, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SuccessStatusCode other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Status, Pattern);

    public static bool operator ==(SuccessStatusCode left, SuccessStatusCode right) => left.Equals(right);

    public static bool operator !=(SuccessStatusCode left, SuccessStatusCode right) => !left.Equals(right);

    public override string ToString()
        => Pattern ?? Status?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}

/// <summary>
/// Retry backoff strategy for an endpoint. Serializes as <c>"exponential"</c>,
/// <c>"linear"</c> or <c>"fixed"</c>.
/// </summary>
[JsonConverter(typeof(BackoffTypeConverter))]
public enum BackoffType
{
    Exponential,
    Linear,
    Fixed
}

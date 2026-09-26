using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Json;

/// <summary>
/// The serializer settings the SDK uses for every request body and response it handles.
/// </summary>
/// <remarks>
/// Exposed so that callers (and the SDK's own tests) can reproduce the exact bytes that go on
/// the wire. The instance is read-only; clone it with
/// <c>new JsonSerializerOptions(HookbaseJson.Options)</c> if you need to tweak it.
/// </remarks>
public static class HookbaseJson
{
    static HookbaseJson()
    {
        Options.MakeReadOnly(populateMissingResolver: true);
    }

    /// <summary>The SDK's wire serializer settings.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}

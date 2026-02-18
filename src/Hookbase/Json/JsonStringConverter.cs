using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Json;

/// <summary>
/// Converter for fields that D1/SQLite stores as JSON strings but should deserialize as Dictionary.
/// Handles both actual JSON objects and JSON-encoded strings (e.g., "{\"key\":\"value\"}").
/// </summary>
public class JsonStringDictionaryConverter : JsonConverter<Dictionary<string, object>?>
{
    public override Dictionary<string, object>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrEmpty(str))
                return null;
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(str, options);
            }
            catch
            {
                return null;
            }
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var dict = new Dictionary<string, object>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString()!,
                    JsonValueKind.Number => prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => prop.Value.GetRawText()
                };
            }
            return dict;
        }

        // Skip unexpected tokens
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object>? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}

/// <summary>
/// Converter for fields that D1/SQLite stores as JSON strings but should deserialize as Dictionary&lt;string, string&gt;.
/// </summary>
public class JsonStringStringDictionaryConverter : JsonConverter<Dictionary<string, string>?>
{
    public override Dictionary<string, string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrEmpty(str))
                return null;
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(str, options);
            }
            catch
            {
                return null;
            }
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var dict = new Dictionary<string, string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ToString();
            }
            return dict;
        }

        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, string>? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}

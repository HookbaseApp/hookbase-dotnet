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

/// <summary>
/// Reads any JSON value into a string: a JSON string as its text, anything else (an object, an
/// array, a number) as its raw JSON. For fields the API stores as JSONB but takes as a JSON-encoded
/// string on input, such as an event type's <c>examplePayload</c>.
/// </summary>
public class JsonRawStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString();

        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}

/// <summary>
/// Reads a value the API sends either as JSON or as JSON-encoded text (a <c>text</c> column a
/// plpgsql function returns unparsed, such as a route's <c>filterConditions</c>): a string is
/// parsed as JSON into <typeparamref name="T"/>, anything else is read directly. Text that is
/// not valid JSON for <typeparamref name="T"/> reads as null instead of failing the response.
/// Writes <typeparamref name="T"/> as plain JSON.
/// </summary>
public class JsonTextOrValueConverter<T> : JsonConverter<T?> where T : class
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return null;
            try
            {
                return JsonSerializer.Deserialize<T>(text, options);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}

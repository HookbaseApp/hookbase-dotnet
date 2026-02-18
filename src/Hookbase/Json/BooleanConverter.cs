using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Json;

/// <summary>
/// JSON converter that handles boolean values that may be represented as 0/1 integers or true/false.
/// This is needed because the Hookbase API returns SQLite boolean fields as integers in some endpoints.
/// </summary>
public class BooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Number:
                var number = reader.GetInt32();
                return number != 0;
            case JsonTokenType.String:
                var str = reader.GetString();
                if (bool.TryParse(str, out var boolValue))
                    return boolValue;
                if (int.TryParse(str, out var intValue))
                    return intValue != 0;
                throw new JsonException($"Unable to convert \"{str}\" to Boolean.");
            default:
                throw new JsonException($"Unexpected token type {reader.TokenType} when parsing Boolean.");
        }
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}

/// <summary>
/// JSON converter for nullable boolean values.
/// </summary>
public class NullableBooleanConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Number:
                var number = reader.GetInt32();
                return number != 0;
            case JsonTokenType.String:
                var str = reader.GetString();
                if (string.IsNullOrEmpty(str))
                    return null;
                if (bool.TryParse(str, out var boolValue))
                    return boolValue;
                if (int.TryParse(str, out var intValue))
                    return intValue != 0;
                throw new JsonException($"Unable to convert \"{str}\" to Boolean.");
            default:
                throw new JsonException($"Unexpected token type {reader.TokenType} when parsing Boolean.");
        }
    }

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteBooleanValue(value.Value);
        else
            writer.WriteNullValue();
    }
}

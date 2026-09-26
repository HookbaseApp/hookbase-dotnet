using Hookbase.Models.Endpoints;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hookbase.Json;

/// <summary>
/// Converter for <see cref="EndpointHeaderList"/>. Always writes the array-of-objects shape the
/// API accepts, and reads either a JSON array or a JSON-encoded string (D1/SQLite stores the
/// column as text, so some endpoints return it that way).
/// </summary>
public class EndpointHeaderListConverter : JsonConverter<EndpointHeaderList>
{
    public override EndpointHeaderList? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString();
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            try
            {
                var fromString = JsonSerializer.Deserialize<List<EndpointHeader>>(raw, options);
                return fromString == null ? null : new EndpointHeaderList(fromString);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var headers = new EndpointHeaderList();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var header = JsonSerializer.Deserialize<EndpointHeader>(ref reader, options);
                if (header != null)
                {
                    headers.Add(header);
                }
            }
            return headers;
        }

        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, EndpointHeaderList value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var header in value)
        {
            writer.WriteStartObject();
            writer.WriteString("name", header.Name);
            writer.WriteString("value", header.Value);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }
}

/// <summary>
/// Converter for <see cref="SuccessStatusCode"/>. Numeric entries are written as JSON numbers and
/// pattern entries as JSON strings, which is what the API's <c>number | string</c> union expects.
/// </summary>
public class SuccessStatusCodeConverter : JsonConverter<SuccessStatusCode>
{
    public override SuccessStatusCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return SuccessStatusCode.FromStatus(reader.GetInt32());
            case JsonTokenType.String:
                return SuccessStatusCode.FromPattern(reader.GetString() ?? string.Empty);
            default:
                throw new JsonException(
                    $"Unexpected token type {reader.TokenType} when parsing a success status code; expected a number or a string.");
        }
    }

    public override void Write(Utf8JsonWriter writer, SuccessStatusCode value, JsonSerializerOptions options)
    {
        if (value.Status.HasValue)
        {
            writer.WriteNumberValue(value.Status.Value);
        }
        else if (value.Pattern != null)
        {
            writer.WriteStringValue(value.Pattern);
        }
        else
        {
            throw new JsonException(
                "SuccessStatusCode was not initialized. Use SuccessStatusCode.FromStatus(int) or SuccessStatusCode.FromPattern(string).");
        }
    }
}

/// <summary>
/// Converter for <see cref="BackoffType"/>. Writes the lowercase wire values
/// (<c>exponential</c>, <c>linear</c>, <c>fixed</c>) regardless of the ambient naming policy.
/// </summary>
public class BackoffTypeConverter : JsonConverter<BackoffType>
{
    public override BackoffType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Unexpected token type {reader.TokenType} when parsing a backoff type.");
        }

        var raw = reader.GetString();
        return raw?.ToLowerInvariant() switch
        {
            "exponential" => BackoffType.Exponential,
            "linear" => BackoffType.Linear,
            "fixed" => BackoffType.Fixed,
            _ => throw new JsonException($"Unable to convert \"{raw}\" to a backoff type.")
        };
    }

    public override void Write(Utf8JsonWriter writer, BackoffType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            BackoffType.Exponential => "exponential",
            BackoffType.Linear => "linear",
            BackoffType.Fixed => "fixed",
            _ => throw new JsonException($"Unknown backoff type '{value}'.")
        });
    }
}

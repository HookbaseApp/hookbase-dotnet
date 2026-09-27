using Hookbase.Models.Endpoints;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Hookbase.Json;

/// <summary>
/// The canonical wire body for <c>POST /api/webhook-endpoints</c> and
/// <c>PATCH /api/webhook-endpoints/:id</c>.
/// </summary>
/// <remarks>
/// Every key is spelled explicitly and omitted when null, so the serialized body is identical
/// no matter which <see cref="JsonSerializerOptions"/> the caller happens to use. The API
/// schemas are about to become <c>.strict()</c>: any key that is not listed here is rejected,
/// which is why the request records funnel through this type instead of being serialized
/// property-by-property.
/// </remarks>
internal sealed class EndpointRequestPayload
{
    [JsonPropertyName("applicationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApplicationId { get; set; }

    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("headers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EndpointHeaderList? Headers { get; set; }

    [JsonPropertyName("timeoutSeconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeoutSeconds { get; set; }

    [JsonPropertyName("rateLimitPerSecond")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RateLimitPerSecond { get; set; }

    [JsonPropertyName("successStatusCodes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SuccessStatusCode>? SuccessStatusCodes { get; set; }

    [JsonPropertyName("backoffType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BackoffType? BackoffType { get; set; }

    [JsonPropertyName("retryDelays")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? RetryDelays { get; set; }

    [JsonPropertyName("ipAllowlistNotes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IpAllowlistNotes { get; set; }

    [JsonPropertyName("useStaticIp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseStaticIp { get; set; }

    [JsonPropertyName("circuitFailureThreshold")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CircuitFailureThreshold { get; set; }

    [JsonPropertyName("circuitSuccessThreshold")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CircuitSuccessThreshold { get; set; }

    [JsonPropertyName("circuitCooldownSeconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CircuitCooldownSeconds { get; set; }

    [JsonPropertyName("isDisabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsDisabled { get; set; }

    [JsonPropertyName("disabledReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisabledReason { get; set; }

    // Legacy members are read here deliberately so their values are forwarded to the
    // replacement field instead of being silently dropped.
#pragma warning disable CS0618
    internal static EndpointRequestPayload FromCreate(CreateEndpointRequest request) => new()
    {
        ApplicationId = request.ApplicationId,
        Url = request.Url,
        Description = request.Description,
        Headers = request.Headers,
        TimeoutSeconds = request.TimeoutSeconds,
        RateLimitPerSecond = request.RateLimitPerSecond ?? request.RateLimit,
        SuccessStatusCodes = request.SuccessStatusCodes,
        BackoffType = request.BackoffType,
        RetryDelays = request.RetryDelays,
        IpAllowlistNotes = request.IpAllowlistNotes,
        UseStaticIp = request.UseStaticIp,
        CircuitFailureThreshold = request.CircuitFailureThreshold,
        CircuitSuccessThreshold = request.CircuitSuccessThreshold,
        CircuitCooldownSeconds = request.CircuitCooldownSeconds
    };

    internal static EndpointRequestPayload FromUpdate(UpdateEndpointRequest request) => new()
    {
        Url = request.Url,
        Description = request.Description,
        Headers = request.Headers,
        TimeoutSeconds = request.TimeoutSeconds,
        RateLimitPerSecond = request.RateLimitPerSecond ?? request.RateLimit,
        SuccessStatusCodes = request.SuccessStatusCodes,
        BackoffType = request.BackoffType,
        RetryDelays = request.RetryDelays,
        IpAllowlistNotes = request.IpAllowlistNotes,
        UseStaticIp = request.UseStaticIp,
        CircuitFailureThreshold = request.CircuitFailureThreshold,
        CircuitSuccessThreshold = request.CircuitSuccessThreshold,
        CircuitCooldownSeconds = request.CircuitCooldownSeconds,
        IsDisabled = request.IsDisabled,
        DisabledReason = request.DisabledReason
    };
#pragma warning restore CS0618

    internal CreateEndpointRequest ToCreate() => new()
    {
        ApplicationId = ApplicationId ?? string.Empty,
        Url = Url ?? string.Empty,
        Description = Description,
        Headers = Headers,
        TimeoutSeconds = TimeoutSeconds,
        RateLimitPerSecond = RateLimitPerSecond,
        SuccessStatusCodes = SuccessStatusCodes,
        BackoffType = BackoffType,
        RetryDelays = RetryDelays,
        IpAllowlistNotes = IpAllowlistNotes,
        UseStaticIp = UseStaticIp,
        CircuitFailureThreshold = CircuitFailureThreshold,
        CircuitSuccessThreshold = CircuitSuccessThreshold,
        CircuitCooldownSeconds = CircuitCooldownSeconds
    };

    internal UpdateEndpointRequest ToUpdate() => new()
    {
        Url = Url,
        Description = Description,
        Headers = Headers,
        TimeoutSeconds = TimeoutSeconds,
        RateLimitPerSecond = RateLimitPerSecond,
        SuccessStatusCodes = SuccessStatusCodes,
        BackoffType = BackoffType,
        RetryDelays = RetryDelays,
        IpAllowlistNotes = IpAllowlistNotes,
        UseStaticIp = UseStaticIp,
        CircuitFailureThreshold = CircuitFailureThreshold,
        CircuitSuccessThreshold = CircuitSuccessThreshold,
        CircuitCooldownSeconds = CircuitCooldownSeconds,
        IsDisabled = IsDisabled,
        DisabledReason = DisabledReason
    };
}

/// <summary>
/// Serializes <see cref="CreateEndpointRequest"/> into the exact body
/// <c>POST /api/webhook-endpoints</c> accepts, folding legacy members into their replacements
/// and dropping members the API never accepted.
/// </summary>
public class CreateEndpointRequestConverter : JsonConverter<CreateEndpointRequest>
{
    public override CreateEndpointRequest? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<EndpointRequestPayload>(ref reader, options)?.ToCreate();

    public override void Write(Utf8JsonWriter writer, CreateEndpointRequest value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, EndpointRequestPayload.FromCreate(value), options);
}

/// <summary>
/// Serializes <see cref="UpdateEndpointRequest"/> into the exact body
/// <c>PATCH /api/webhook-endpoints/:id</c> accepts, folding legacy members into their
/// replacements and dropping members the API never accepted.
/// </summary>
public class UpdateEndpointRequestConverter : JsonConverter<UpdateEndpointRequest>
{
    /// <summary>The wire key each clearable field is nulled under.</summary>
    private static readonly Dictionary<EndpointField, string> ClearableFields = new()
    {
        [EndpointField.Description] = "description",
        [EndpointField.SuccessStatusCodes] = "successStatusCodes",
        [EndpointField.BackoffType] = "backoffType",
        [EndpointField.RetryDelays] = "retryDelays",
        [EndpointField.IpAllowlistNotes] = "ipAllowlistNotes"
    };

    public override UpdateEndpointRequest? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<EndpointRequestPayload>(ref reader, options)?.ToUpdate();

    public override void Write(Utf8JsonWriter writer, UpdateEndpointRequest value, JsonSerializerOptions options)
    {
        var payload = EndpointRequestPayload.FromUpdate(value);

        if (value.Clear.Count == 0)
        {
            JsonSerializer.Serialize(writer, payload, options);
            return;
        }

        // Serialize to a node and add the nulls to it, rather than hand-writing the body: the
        // payload type stays the single list of keys the API accepts, and a field added there is
        // carried here without a second copy of it to keep in step.
        var body = JsonSerializer.SerializeToNode(payload, options)?.AsObject()
            ?? throw new InvalidOperationException("Serializing the endpoint update body produced no object.");

        foreach (var field in value.Clear)
        {
            if (!ClearableFields.TryGetValue(field, out var key))
            {
                throw new InvalidOperationException(
                    $"UpdateEndpointRequest.Clear names {field}, which the Hookbase API does not accept " +
                    "a null for. Clearable fields: " + string.Join(", ", ClearableFields.Keys) + ".");
            }

            if (body.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"UpdateEndpointRequest.Clear names {field} but that field is also set on the same " +
                    "request; clear it or set it, not both.");
            }

            body[key] = null;
        }

        body.WriteTo(writer, options);
    }
}

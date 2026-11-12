using System.Text.Json;

namespace Hookbase.Models.Deliveries;

/// <summary>
/// Edits for replaying a delivery (<c>POST /api/deliveries/:id/replay</c>), passed to
/// <c>DeliveriesResource.ReplayAsync(id, request)</c>. Every member is optional and a null one is
/// not sent; with none set the replay re-sends the stored payload as it was.
/// </summary>
/// <remarks>
/// Set at most one of <see cref="ModifiedPayload"/> and <see cref="PayloadPatch"/>; the SDK throws
/// <see cref="ArgumentException"/> before sending when both are set. A replay that edits what
/// reaches a bridge destination also needs the <c>events:send</c> permission.
/// </remarks>
public record ReplayDeliveryRequest
{
    /// <summary>Replace the payload with this JSON.</summary>
    public JsonElement? ModifiedPayload { get; init; }

    /// <summary>
    /// Edit the stored payload with this JSON Merge Patch (RFC 7386): a JSON object whose members
    /// replace the payload's, with <c>null</c> removing one. The payload must be a JSON object, and a
    /// patch cannot touch a field the source encrypts.
    /// </summary>
    public JsonElement? PayloadPatch { get; init; }

    /// <summary>Send the replay to this destination (ID or slug) instead of the original one.</summary>
    public string? DestinationOverride { get; init; }

    /// <summary>Run this transform on the replay instead of the route's.</summary>
    public ReplayTransformOverride? TransformOverride { get; init; }

    /// <summary>
    /// Headers to set on the replayed request, at most 50. Framing and signature headers are
    /// reserved and refused.
    /// </summary>
    public Dictionary<string, string>? HeadersOverride { get; init; }

    /// <summary>
    /// Also save <see cref="TransformOverride"/> as the route's transform (which the route must
    /// already have). Needs <see cref="TransformOverride"/>, and the <c>transforms:update</c> permission.
    /// </summary>
    public bool? PersistTransform { get; init; }
}

/// <summary>A transform to run on one replay (<see cref="ReplayDeliveryRequest.TransformOverride"/>).</summary>
public record ReplayTransformOverride
{
    public required string Code { get; init; }

    /// <summary>
    /// Transform language, e.g. <c>jsonata</c>, <c>javascript</c>, <c>jq</c>; null uses the route
    /// transform's (see <c>TransformsResource.EnginesAsync</c>).
    /// </summary>
    public string? Type { get; init; }

    /// <summary><c>json</c>, <c>xml</c> or <c>text</c>.</summary>
    public string? InputFormat { get; init; }

    /// <summary><c>json</c>, <c>xml</c> or <c>text</c>.</summary>
    public string? OutputFormat { get; init; }
}

/// <summary>The queued replay (<c>DeliveriesResource.ReplayAsync(id, request)</c>).</summary>
public record ReplayDeliveryResult
{
    /// <summary>The new delivery. The replayed one keeps its status.</summary>
    public string? DeliveryId { get; init; }

    /// <summary>e.g. <c>Replay queued with modified payload</c>.</summary>
    public string? Message { get; init; }

    /// <summary>Whether the payload was replaced or patched.</summary>
    public bool Modified { get; init; }

    /// <summary>Whether the replay goes to a different destination than the original.</summary>
    public bool DestinationOverridden { get; init; }

    public bool TransformOverridden { get; init; }

    /// <summary>Whether the transform override was saved to the route.</summary>
    public bool TransformPersisted { get; init; }

    public bool HeadersOverridden { get; init; }

    /// <summary>The destination the replay is sent to.</summary>
    public string? TargetDestinationId { get; init; }
}

using System.Security.Cryptography;
using System.Text;
using Hookbase.Exceptions;
using Hookbase.Webhooks;
using Xunit;

namespace Hookbase.Tests;

public class WebhookVerifierTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] OtherKey = Enumerable.Range(100, 32).Select(i => (byte)i).ToArray();
    private const string Payload = "{\"type\":\"order.created\",\"id\":42}";
    private const string MessageId = "264e74b1-f7ff-4dbf-9e37-54a4763d961d";

    private static string Sign(byte[] key, string id, string timestamp, string payload)
    {
        using var hmac = new HMACSHA256(key);
        return "v1," + Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{id}.{timestamp}.{payload}")));
    }

    private static Dictionary<string, string> Headers(string signature, string? timestamp = null) => new()
    {
        ["Webhook-Id"] = MessageId,
        ["Webhook-Timestamp"] = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
        ["Webhook-Signature"] = signature,
    };

    private static string Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    [Fact]
    public void Accepts_a_single_signature()
    {
        var ts = Now();
        var verifier = new WebhookVerifier("whsec_" + Convert.ToBase64String(Key));
        Assert.True(verifier.VerifySignature(Payload, Headers(Sign(Key, MessageId, ts, Payload), ts)));
    }

    [Fact]
    public void Accepts_either_signature_during_a_rotation_grace_period()
    {
        var ts = Now();
        var header = $"{Sign(Key, MessageId, ts, Payload)} {Sign(OtherKey, MessageId, ts, Payload)}";

        Assert.True(new WebhookVerifier("whsec_" + Convert.ToBase64String(Key)).VerifySignature(Payload, Headers(header, ts)));
        Assert.True(new WebhookVerifier("whsec_" + Convert.ToBase64String(OtherKey)).VerifySignature(Payload, Headers(header, ts)));
    }

    [Fact]
    public void Rejects_a_signature_from_another_secret()
    {
        var ts = Now();
        var verifier = new WebhookVerifier("whsec_" + Convert.ToBase64String(Key));
        Assert.False(verifier.VerifySignature(Payload, Headers(Sign(OtherKey, MessageId, ts, Payload), ts)));
    }

    [Fact]
    public void Rejects_a_tampered_payload()
    {
        var ts = Now();
        var verifier = new WebhookVerifier("whsec_" + Convert.ToBase64String(Key));
        Assert.False(verifier.VerifySignature(Payload + " ", Headers(Sign(Key, MessageId, ts, Payload), ts)));
    }

    [Fact]
    public void Ignores_entries_that_are_not_v1()
    {
        var ts = Now();
        var sig = Sign(Key, MessageId, ts, Payload).Substring(3);
        var verifier = new WebhookVerifier("whsec_" + Convert.ToBase64String(Key));
        Assert.False(verifier.VerifySignature(Payload, Headers($"v1a,{sig} {sig}", ts)));
    }

    [Fact]
    public void Accepts_operational_webhook_secrets()
    {
        var ts = Now();
        var verifier = new WebhookVerifier("whsec_op_" + Convert.ToBase64String(Key));
        Assert.True(verifier.VerifySignature(Payload, Headers(Sign(Key, MessageId, ts, Payload), ts)));
    }

    [Fact]
    public void Accepts_legacy_hex_secrets()
    {
        var ts = Now();
        var verifier = new WebhookVerifier("whsec_" + Convert.ToHexString(Key).ToLowerInvariant());
        Assert.True(verifier.VerifySignature(Payload, Headers(Sign(Key, MessageId, ts, Payload), ts)));
    }

    [Fact]
    public void Rejects_timestamps_outside_the_tolerance_in_either_direction()
    {
        var verifier = new WebhookVerifier("whsec_" + Convert.ToBase64String(Key));
        foreach (var offset in new[] { -600, 600 })
        {
            var ts = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + offset).ToString();
            var ex = Assert.Throws<WebhookVerificationException>(
                () => verifier.Verify<Dictionary<string, object>>(Payload, Headers(Sign(Key, MessageId, ts, Payload), ts)));
            Assert.Contains("timestamp", ex.Message);
        }
    }

    [Fact]
    public void Deserializes_the_verified_payload()
    {
        var ts = Now();
        var verifier = new WebhookVerifier("whsec_" + Convert.ToBase64String(Key));
        var result = verifier.Verify<Dictionary<string, object>>(Payload, Headers(Sign(Key, MessageId, ts, Payload), ts));
        Assert.Equal("order.created", result["type"].ToString());
    }
}

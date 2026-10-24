using Hookbase.Exceptions;
using Hookbase.Webhooks;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Fixed vectors from the API signer (api/src/utils/outbound-signature.ts signOutboundPayload, run
/// under Node) and the Standard Webhooks reference vector, plus malformed-input handling. The
/// timestamps are fixed, so the tolerance is widened.
/// </summary>
public class WebhookInteropAndInputTests
{
    private const string SpecSecret = "whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw";
    private static readonly TimeSpan Forever = TimeSpan.FromDays(365 * 100);

    private static Dictionary<string, string> Headers(string id, string timestamp, string signature) => new()
    {
        ["webhook-id"] = id,
        ["webhook-timestamp"] = timestamp,
        ["webhook-signature"] = signature,
    };

    private static string Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    [Fact]
    public void Standard_Webhooks_reference_vector_which_the_API_signer_reproduces()
    {
        var verifier = new WebhookVerifier(SpecSecret);
        var headers = Headers("msg_p5jXN8AQM9LWM0D4loKWxJek", "1614265330", "v1,g0hM9SsE+OTPJTGt/tmIKtSyZlE3uFJELVlNIOLJ1OE=");

        Assert.True(verifier.VerifySignature("{\"test\": 2432232314}", headers, Forever));
        Assert.False(verifier.VerifySignature("{\"test\": 2432232315}", headers, Forever));

        var generated = verifier.GenerateTestHeaders("{\"test\": 2432232314}", "msg_p5jXN8AQM9LWM0D4loKWxJek",
            DateTimeOffset.FromUnixTimeSeconds(1614265330));
        Assert.Equal(headers["webhook-signature"], generated["webhook-signature"]);
    }

    [Fact]
    public void API_dual_signed_rotation_header_verifies_with_either_secret()
    {
        // signOutboundPayload(..., secret = SpecSecret, previousSecret = whsec_op_<bytes 1..32>):
        // previous first, current second; non-ASCII body pins UTF-8.
        const string header = "v1,Y6hpmiCotZcqtOf/dgG5KARpeTgXngac9wBOMk/vcAU= v1,qKP4SzATWLYZrd+IgPQDYbv3vn2xSk5S3WIWD9HZlE4=";
        const string payload = "{\"note\":\"café ✓\"}";
        var headers = Headers("msg_rot", "1700000000", header);

        Assert.True(new WebhookVerifier(SpecSecret).VerifySignature(payload, headers, Forever));
        Assert.True(new WebhookVerifier("whsec_op_AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=").VerifySignature(payload, headers, Forever));
        Assert.False(new WebhookVerifier("whsec_" + Convert.ToBase64String(new byte[32])).VerifySignature(payload, headers, Forever));
    }

    [Fact]
    public void API_signature_with_a_legacy_hex_secret()
    {
        const string hex = "whsec_0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20";
        var headers = Headers("msg_hex", "1700000000", "v1,lr9mrarVPLrXzQN2UluMykHR4G3gh32rVpBJ1F5FeKM=");

        Assert.True(new WebhookVerifier(hex).VerifySignature("{\"a\":1}", headers, Forever));
        // Same key bytes as base64 sign identically.
        Assert.True(new WebhookVerifier("whsec_AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=").VerifySignature("{\"a\":1}", headers, Forever));
    }

    [Theory]
    [InlineData("webhook-id", "Missing webhook-id header")]
    [InlineData("webhook-timestamp", "Missing webhook-timestamp header")]
    [InlineData("webhook-signature", "Missing webhook-signature header")]
    public void A_missing_header_is_reported(string header, string message)
    {
        var verifier = new WebhookVerifier(SpecSecret);
        var headers = verifier.GenerateTestHeaders("{}");
        headers.Remove(header);

        var ex = Assert.Throws<WebhookVerificationException>(() => verifier.Verify<Dictionary<string, object>>("{}", headers));
        Assert.Equal(message, ex.Message);
        Assert.False(verifier.VerifySignature("{}", headers));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData(" 1700000000")]
    [InlineData("99999999999999")]
    [InlineData("9223372036854775807")]
    public void A_malformed_or_unrepresentable_timestamp_fails_verification_without_throwing(string timestamp)
    {
        var verifier = new WebhookVerifier(SpecSecret);
        var headers = Headers("msg_1", timestamp, "v1,AAAA");

        var ex = Assert.Throws<WebhookVerificationException>(() => verifier.Verify<Dictionary<string, object>>("{}", headers));
        Assert.Equal("Invalid webhook-timestamp format", ex.Message);
        Assert.False(verifier.VerifySignature("{}", headers));
    }

    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData("not json at all")]
    [InlineData("\"a string\"")]
    [InlineData("")]
    public void VerifySignature_checks_only_the_signature_whatever_the_body(string payload)
    {
        var verifier = new WebhookVerifier(SpecSecret);
        Assert.True(verifier.VerifySignature(payload, verifier.GenerateTestHeaders(payload)));
    }

    [Fact]
    public void Verify_of_a_non_JSON_body_with_a_valid_signature_reports_the_deserialization()
    {
        var verifier = new WebhookVerifier(SpecSecret);
        var ex = Assert.Throws<WebhookVerificationException>(
            () => verifier.Verify<Dictionary<string, object>>("nope", verifier.GenerateTestHeaders("nope")));
        Assert.StartsWith("Failed to deserialize payload", ex.Message);
    }

    [Fact]
    public void Verify_deserializes_into_a_typed_model_case_insensitively()
    {
        var verifier = new WebhookVerifier(SpecSecret);
        const string payload = "{\"type\":\"order.created\",\"data\":{\"id\":\"ord_1\"}}";
        var evt = verifier.Verify<TypedEvent>(payload, verifier.GenerateTestHeaders(payload));
        Assert.Equal("order.created", evt.Type);
        Assert.Equal("ord_1", evt.Data!.Id);
    }

    private sealed class TypedEvent
    {
        public string? Type { get; set; }
        public TypedData? Data { get; set; }
    }

    private sealed class TypedData
    {
        public string? Id { get; set; }
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v1,")]
    [InlineData(",abc")]
    [InlineData("   ")]
    [InlineData("v2,abc")]
    public void A_header_with_no_usable_v1_entry_says_so(string signature)
    {
        var verifier = new WebhookVerifier(SpecSecret);
        var ex = Assert.Throws<WebhookVerificationException>(
            () => verifier.Verify<Dictionary<string, object>>("{}", Headers("msg_1", Now(), signature)));
        Assert.Equal("No signatures found in webhook-signature header", ex.Message);
    }

    [Fact]
    public void Junk_entries_alongside_a_valid_one_do_not_block_it()
    {
        var verifier = new WebhookVerifier(SpecSecret);
        var headers = verifier.GenerateTestHeaders("{}");
        headers["webhook-signature"] = "v1,not-base64!! v1a,xyz " + headers["webhook-signature"] + " v1,AAAA";
        Assert.True(verifier.VerifySignature("{}", headers));
    }

    [Fact]
    public void A_custom_tolerance_is_applied()
    {
        var verifier = new WebhookVerifier(SpecSecret);
        var headers = verifier.GenerateTestHeaders("{}", timestamp: DateTimeOffset.UtcNow.AddSeconds(-90));

        Assert.True(verifier.VerifySignature("{}", headers));
        Assert.False(verifier.VerifySignature("{}", headers, TimeSpan.FromSeconds(60)));
        Assert.True(verifier.VerifySignature("{}", headers, TimeSpan.FromSeconds(120)));
    }

    [Fact]
    public void Headers_are_matched_case_insensitively()
    {
        var verifier = new WebhookVerifier(SpecSecret);
        var headers = verifier.GenerateTestHeaders("{}")
            .ToDictionary(h => h.Key.ToUpperInvariant(), h => h.Value);
        Assert.True(verifier.VerifySignature("{}", headers));
    }

    [Theory]
    [InlineData("whsec_not base64!")]
    [InlineData("whsec_abc")]
    [InlineData("whpk_###")]
    [InlineData("whpk_AAAA")]
    [InlineData("whsk_AAAA")]
    public void A_bad_key_format_throws_ArgumentException(string key)
    {
        Assert.Throws<ArgumentException>(() => new WebhookVerifier(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_key_throws_ArgumentException(string key)
    {
        Assert.Throws<ArgumentException>(() => new WebhookVerifier(key));
    }

    [Fact]
    public void A_bad_key_error_never_echoes_the_key()
    {
        const string key = "whsec_SECRETMATERIAL!!";
        var ex = Assert.Throws<ArgumentException>(() => new WebhookVerifier(key));
        Assert.DoesNotContain("SECRETMATERIAL", ex.Message);
    }

    [Fact]
    public void Surrounding_whitespace_on_the_key_is_ignored()
    {
        var verifier = new WebhookVerifier("  " + SpecSecret + "\n");
        var headers = Headers("msg_p5jXN8AQM9LWM0D4loKWxJek", "1614265330", "v1,g0hM9SsE+OTPJTGt/tmIKtSyZlE3uFJELVlNIOLJ1OE=");
        Assert.True(verifier.VerifySignature("{\"test\": 2432232314}", headers, Forever));
        Assert.Equal(WebhookVerifier.HmacSha256, verifier.SignatureScheme);
    }
}

using System.Text;
using Hookbase.Exceptions;
using Hookbase.Webhooks;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Standard Webhooks v1a (Ed25519) verification, signing the way api/src/utils/outbound-signature.ts
/// does: Ed25519 over "{webhook-id}.{webhook-timestamp}.{body}", header entry "v1a,&lt;base64&gt;",
/// public key handed out as "whpk_&lt;base64 of 32 bytes&gt;".
/// </summary>
public class Ed25519WebhookVerifierTests
{
    private const string Payload = "{\"type\":\"order.created\",\"id\":42}";
    private const string MessageId = "msg_2mXq9Vt3hX1aZ8cK";

    private static readonly Ed25519PrivateKeyParameters Key = NewKey();
    private static readonly Ed25519PrivateKeyParameters OtherKey = NewKey();

    private static Ed25519PrivateKeyParameters NewKey()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        return (Ed25519PrivateKeyParameters)generator.GenerateKeyPair().Private;
    }

    private static string PublicKey(Ed25519PrivateKeyParameters key) =>
        "whpk_" + Convert.ToBase64String(key.GeneratePublicKey().GetEncoded());

    private static string Sign(Ed25519PrivateKeyParameters key, string id, string timestamp, string payload)
    {
        var data = Encoding.UTF8.GetBytes($"{id}.{timestamp}.{payload}");
        var signer = new Ed25519Signer();
        signer.Init(true, key);
        signer.BlockUpdate(data, 0, data.Length);
        return "v1a," + Convert.ToBase64String(signer.GenerateSignature());
    }

    private static string HmacSign(byte[] secret, string id, string timestamp, string payload)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(secret);
        return "v1," + Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{id}.{timestamp}.{payload}")));
    }

    private static Dictionary<string, string> Headers(string signature, string timestamp, string id = MessageId) => new()
    {
        ["webhook-id"] = id,
        ["webhook-timestamp"] = timestamp,
        ["webhook-signature"] = signature,
    };

    private static string Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    [Fact]
    public void A_whpk_key_selects_the_ed25519_scheme()
    {
        Assert.Equal(WebhookVerifier.Ed25519, new WebhookVerifier(PublicKey(Key)).SignatureScheme);
        Assert.Equal("ed25519", WebhookVerifier.Ed25519);
        Assert.Equal(WebhookVerifier.HmacSha256, new WebhookVerifier("whsec_" + Convert.ToBase64String(new byte[32])).SignatureScheme);
    }

    [Fact]
    public void Accepts_a_v1a_signature_and_deserializes()
    {
        var ts = Now();
        var verifier = new WebhookVerifier(PublicKey(Key));
        var result = verifier.Verify<Dictionary<string, object>>(Payload, Headers(Sign(Key, MessageId, ts, Payload), ts));
        Assert.Equal("order.created", result["type"].ToString());
    }

    [Fact]
    public void Verifies_a_signature_produced_by_the_server_webcrypto_code_path()
    {
        // Generated with Node 22 WebCrypto exactly as outbound-signature.ts does (key pair exported to
        // whsk_/whpk_, whsk_ re-imported as a JWK, crypto.subtle.sign('Ed25519', ...)). Fixed
        // timestamp, so the tolerance is widened; the payload has non-ASCII to pin UTF-8 encoding.
        const string whpk = "whpk_a7kJGlsS6YsGk3vSm+i9CnE5krydSheW91/PIrZ0B38=";
        const string payload = "{\"type\":\"order.created\",\"data\":{\"id\":\"ord_1\",\"note\":\"café ✓\"}}";
        const string signature = "v1a,7LcI9MK+cWjiZBdWdyzi48fk0WzGSl7XCPpDhwhltUWImAsmzvNLtA560Lkibj/2s109CUsYSKoS6A7Xw/7+Ag==";
        var verifier = new WebhookVerifier(whpk);
        var tolerance = TimeSpan.FromDays(365 * 100);

        Assert.True(verifier.VerifySignature(payload, Headers(signature, "1761091200", "msg_2mXq9Vt3hX1aZ8cK"), tolerance));
        Assert.False(verifier.VerifySignature(payload, Headers(signature, "1761091201", "msg_2mXq9Vt3hX1aZ8cK"), tolerance));
    }

    [Fact]
    public void Rejects_a_signature_from_another_key()
    {
        var ts = Now();
        var verifier = new WebhookVerifier(PublicKey(Key));
        var ex = Assert.Throws<WebhookVerificationException>(
            () => verifier.Verify<Dictionary<string, object>>(Payload, Headers(Sign(OtherKey, MessageId, ts, Payload), ts)));
        Assert.Equal("Signature verification failed", ex.Message);
    }

    [Fact]
    public void Rejects_a_tampered_payload_id_or_timestamp()
    {
        var ts = Now();
        var sig = Sign(Key, MessageId, ts, Payload);
        var verifier = new WebhookVerifier(PublicKey(Key));

        Assert.False(verifier.VerifySignature(Payload + " ", Headers(sig, ts)));
        Assert.False(verifier.VerifySignature(Payload, Headers(sig, ts, "msg_other")));
        Assert.False(verifier.VerifySignature(Payload, Headers(sig, (long.Parse(ts) - 1).ToString())));
    }

    [Fact]
    public void Rejects_timestamps_outside_the_tolerance_in_either_direction()
    {
        var verifier = new WebhookVerifier(PublicKey(Key));
        foreach (var offset in new[] { -600, 600 })
        {
            var ts = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + offset).ToString();
            var ex = Assert.Throws<WebhookVerificationException>(
                () => verifier.Verify<Dictionary<string, object>>(Payload, Headers(Sign(Key, MessageId, ts, Payload), ts)));
            Assert.Contains("timestamp", ex.Message);
        }

        // ...and a custom tolerance admits the same age.
        var old = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 600).ToString();
        Assert.True(verifier.VerifySignature(Payload, Headers(Sign(Key, MessageId, old, Payload), old), TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Accepts_either_key_during_a_rotation_grace_period()
    {
        // The server sends "v1a,<previous> v1a,<current>" while the old key is in its grace period.
        var ts = Now();
        var header = $"{Sign(OtherKey, MessageId, ts, Payload)} {Sign(Key, MessageId, ts, Payload)}";

        Assert.True(new WebhookVerifier(PublicKey(Key)).VerifySignature(Payload, Headers(header, ts)));
        Assert.True(new WebhookVerifier(PublicKey(OtherKey)).VerifySignature(Payload, Headers(header, ts)));
        Assert.False(new WebhookVerifier(PublicKey(NewKey())).VerifySignature(Payload, Headers(header, ts)));
    }

    [Fact]
    public void Mixed_header_each_scheme_checks_only_its_own_entries()
    {
        var ts = Now();
        var hmacSecret = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        var v1 = HmacSign(hmacSecret, MessageId, ts, Payload);
        var v1a = Sign(Key, MessageId, ts, Payload);
        var header = $"{v1} garbage v1a,!!notbase64!! v1a,{Convert.ToBase64String(new byte[10])} {v1a}";

        Assert.True(new WebhookVerifier(PublicKey(Key)).VerifySignature(Payload, Headers(header, ts)));
        Assert.True(new WebhookVerifier("whsec_" + Convert.ToBase64String(hmacSecret)).VerifySignature(Payload, Headers(header, ts)));

        // A v1a value relabelled as v1 is not checked by the Ed25519 verifier.
        Assert.False(new WebhookVerifier(PublicKey(Key)).VerifySignature(Payload, Headers("v1," + v1a.Substring(4), ts)));
    }

    [Fact]
    public void Says_so_when_the_header_has_no_v1a_entries()
    {
        // Typically an HMAC endpoint being verified with a public key.
        var ts = Now();
        var header = HmacSign(new byte[32], MessageId, ts, Payload);
        var ex = Assert.Throws<WebhookVerificationException>(
            () => new WebhookVerifier(PublicKey(Key)).Verify<Dictionary<string, object>>(Payload, Headers(header, ts)));
        Assert.Contains("v1a", ex.Message);
    }

    [Theory]
    [InlineData("whpk_")]
    [InlineData("whpk_not*base64")]
    [InlineData("whpk_AAAA")]
    public void Rejects_malformed_public_keys(string key)
    {
        Assert.Throws<ArgumentException>(() => new WebhookVerifier(key));
    }

    [Fact]
    public void Rejects_a_32_byte_key_that_is_not_a_curve_point()
    {
        // y = 2 has no matching x on edwards25519, so this decodes to no point.
        var bytes = new byte[32];
        bytes[0] = 2;
        Assert.Throws<ArgumentException>(() => new WebhookVerifier("whpk_" + Convert.ToBase64String(bytes)));
    }
}

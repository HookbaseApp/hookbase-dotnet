using Hookbase.Exceptions;
using Hookbase.Webhooks;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// <see cref="WebhookVerifier.GenerateTestHeaders"/>: HMAC (whsec_) and Ed25519 (whsk_ = base64 of
/// seed || public key) headers must pass Verify, and a public-key-only verifier cannot sign.
/// </summary>
public class TestHeaderGenerationTests
{
    private const string Payload = "{\"type\":\"order.created\",\"id\":42}";
    private static readonly string HmacSecret = "whsec_" + Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private static (string SecretKey, string PublicKey) NewEd25519Pair()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        var seed = ((Ed25519PrivateKeyParameters)pair.Private).GetEncoded();
        var pub = ((Ed25519PublicKeyParameters)pair.Public).GetEncoded();
        return ("whsk_" + Convert.ToBase64String(seed.Concat(pub).ToArray()), "whpk_" + Convert.ToBase64String(pub));
    }

    [Fact]
    public void Hmac_headers_round_trip_through_Verify()
    {
        var verifier = new WebhookVerifier(HmacSecret);
        var headers = verifier.GenerateTestHeaders(Payload);

        Assert.StartsWith("msg_", headers["webhook-id"]);
        Assert.StartsWith("v1,", headers["webhook-signature"]);
        var parsed = verifier.Verify<Dictionary<string, object>>(Payload, headers);
        Assert.Equal("order.created", parsed["type"].ToString());
    }

    [Fact]
    public void Uses_the_given_id_and_timestamp()
    {
        var verifier = new WebhookVerifier(HmacSecret);
        var ts = DateTimeOffset.UtcNow.AddSeconds(-30);
        var headers = verifier.GenerateTestHeaders(Payload, "msg_fixed", ts);

        Assert.Equal("msg_fixed", headers["webhook-id"]);
        Assert.Equal(ts.ToUnixTimeSeconds().ToString(), headers["webhook-timestamp"]);
        Assert.True(verifier.VerifySignature(Payload, headers));
    }

    [Fact]
    public void An_old_timestamp_is_rejected_by_Verify()
    {
        var verifier = new WebhookVerifier(HmacSecret);
        var headers = verifier.GenerateTestHeaders(Payload, timestamp: DateTimeOffset.UtcNow.AddHours(-1));
        Assert.Throws<WebhookVerificationException>(() => verifier.Verify<Dictionary<string, object>>(Payload, headers));
    }

    [Fact]
    public void Generates_a_fresh_id_each_call()
    {
        var verifier = new WebhookVerifier(HmacSecret);
        Assert.NotEqual(verifier.GenerateTestHeaders(Payload)["webhook-id"], verifier.GenerateTestHeaders(Payload)["webhook-id"]);
    }

    [Fact]
    public void Whsk_headers_are_v1a_and_verify_with_the_secret_key_and_the_public_key()
    {
        var (secretKey, publicKey) = NewEd25519Pair();
        var signer = new WebhookVerifier(secretKey);
        Assert.Equal(WebhookVerifier.Ed25519, signer.SignatureScheme);

        var headers = signer.GenerateTestHeaders(Payload);
        Assert.StartsWith("v1a,", headers["webhook-signature"]);

        signer.Verify<Dictionary<string, object>>(Payload, headers);
        new WebhookVerifier(publicKey).Verify<Dictionary<string, object>>(Payload, headers);

        // Another key's public half must not accept it, and a tampered body must fail.
        var (_, otherPublic) = NewEd25519Pair();
        Assert.False(new WebhookVerifier(otherPublic).VerifySignature(Payload, headers));
        Assert.False(signer.VerifySignature(Payload + " ", headers));
    }

    [Fact]
    public void Whsk_signs_exactly_as_the_server_does()
    {
        // RFC 8032 7.1 TEST 1 key and the server's signature of `msg_1.1700000000.{"a":1}`
        // (api/src/utils/outbound-signature.workers.test.ts). Ed25519 is deterministic.
        const string secretKey = "whsk_nWGxne/9WmC6hEr0kuwsxERJxWl7MmkZcDusAxyuf2DXWpgBgrEKt9VL/tPJZAc6DuFy89qmIyWvAhpo9wdRGg==";
        const string serverSignature = "0pYc3grQLAYTYYtSm9Z+VOqkyvPhiL3xzHrpcxS6/6sXV93wWnX+gE4ZSgyOhnhCp3VTI/jx2HUyQVOO3T5OCg==";

        var headers = new WebhookVerifier(secretKey).GenerateTestHeaders(
            "{\"a\":1}", "msg_1", DateTimeOffset.FromUnixTimeSeconds(1700000000));

        Assert.Equal("1700000000", headers["webhook-timestamp"]);
        Assert.Equal("v1a," + serverSignature, headers["webhook-signature"]);
    }

    [Fact]
    public void A_whpk_verifier_cannot_generate_headers()
    {
        var (_, publicKey) = NewEd25519Pair();
        var ex = Assert.Throws<InvalidOperationException>(() => new WebhookVerifier(publicKey).GenerateTestHeaders(Payload));
        Assert.Contains("whsk_", ex.Message);
    }

    [Fact]
    public void Rejects_a_malformed_whsk_key_without_echoing_it()
    {
        var (secretKey, _) = NewEd25519Pair();
        var body = secretKey.Substring(5);

        var wrongLength = "whsk_" + Convert.ToBase64String(new byte[32]);
        var ex1 = Assert.Throws<ArgumentException>(() => new WebhookVerifier(wrongLength));
        Assert.DoesNotContain(wrongLength.Substring(5), ex1.Message);

        // Seed of one key with the public half of another.
        var (otherSecret, _) = NewEd25519Pair();
        var a = Convert.FromBase64String(body);
        var b = Convert.FromBase64String(otherSecret.Substring(5));
        var mismatched = "whsk_" + Convert.ToBase64String(a.Take(32).Concat(b.Skip(32)).ToArray());
        var ex2 = Assert.Throws<ArgumentException>(() => new WebhookVerifier(mismatched));
        Assert.DoesNotContain(mismatched.Substring(5), ex2.Message);
        Assert.DoesNotContain(Convert.ToBase64String(a.Take(32).ToArray()), ex2.Message);

        Assert.Throws<ArgumentException>(() => new WebhookVerifier("whsk_not*base64"));
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hookbase.Exceptions;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Hookbase.Webhooks;

/// <summary>
/// Verifies the Standard Webhooks signatures Hookbase sends to your endpoints.
/// </summary>
/// <remarks>
/// <para>
/// The key you construct it with picks the scheme, exactly as the endpoint's
/// <c>signatureScheme</c> does on the server:
/// </para>
/// <list type="bullet">
/// <item><description><c>whsec_...</c> (HMAC-SHA256, scheme <c>hmac-sha256</c>): checks the
/// <c>v1,&lt;base64&gt;</c> entries of <c>webhook-signature</c>.</description></item>
/// <item><description><c>whpk_...</c> (Ed25519 public key, scheme <c>ed25519</c>): checks the
/// <c>v1a,&lt;base64&gt;</c> entries. The public key is <see cref="Models.Endpoints.Endpoint.PublicKey"/>
/// (or <see cref="Models.Endpoints.RotateSecretResult.PublicKey"/> after a rotation); Hookbase keeps
/// the private key and never returns it.</description></item>
/// </list>
/// <para>
/// Both schemes sign <c>{webhook-id}.{webhook-timestamp}.{body}</c>, apply the same timestamp
/// tolerance, and accept the header when any one entry of their version matches (during a key
/// rotation the header carries the previous and the new signature). Entries of the other version
/// are ignored. Instances are immutable and thread-safe.
/// </para>
/// </remarks>
public class WebhookVerifier
{
    /// <summary>Value of <see cref="SignatureScheme"/> for a <c>whsec_</c> secret.</summary>
    public const string HmacSha256 = "hmac-sha256";

    /// <summary>Value of <see cref="SignatureScheme"/> for a <c>whpk_</c> public key.</summary>
    public const string Ed25519 = "ed25519";

    private const string PublicKeyPrefix = "whpk_";

    // Exactly one of these is set, by the constructor.
    private readonly byte[]? _secretKey;
    private readonly Ed25519PublicKeyParameters? _publicKey;

    /// <summary>
    /// The scheme this verifier checks: <see cref="HmacSha256"/> (<c>v1</c> entries) or
    /// <see cref="Ed25519"/> (<c>v1a</c> entries). Same values as the endpoint's <c>signatureScheme</c>.
    /// </summary>
    public string SignatureScheme { get; }

    /// <summary>
    /// Creates a new webhook verifier with the given secret or public key.
    /// </summary>
    /// <param name="secret">The endpoint's verification key: 'whsec_' + base64 (HMAC endpoint secrets),
    /// 'whsec_op_' + base64 (operational webhook secrets), a legacy 64-character hex secret, or
    /// 'whpk_' + base64 of a 32-byte Ed25519 public key (endpoints whose signature scheme is ed25519).</param>
    /// <exception cref="ArgumentException">The key is empty or cannot be decoded.</exception>
    public WebhookVerifier(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret cannot be null or empty", nameof(secret));
        }

        secret = secret.Trim();
        if (secret.StartsWith(PublicKeyPrefix, StringComparison.Ordinal))
        {
            _publicKey = DecodePublicKey(secret.Substring(PublicKeyPrefix.Length));
            SignatureScheme = Ed25519;
            return;
        }

        SignatureScheme = HmacSha256;

        // Strip 'whsec_', plus the 'op_' infix operational webhook secrets carry. base64 never
        // contains '_', so this never trims a regular secret.
        var cleanSecret = secret;
        if (cleanSecret.StartsWith("whsec_", StringComparison.Ordinal))
        {
            cleanSecret = cleanSecret.Substring(6);
            if (cleanSecret.StartsWith("op_", StringComparison.Ordinal))
            {
                cleanSecret = cleanSecret.Substring(3);
            }
        }

        try
        {
            // Legacy secrets are 32 bytes of hex; everything else is base64 (Standard Webhooks).
            _secretKey = IsHex64(cleanSecret)
                ? Convert.FromHexString(cleanSecret)
                : Convert.FromBase64String(cleanSecret);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Invalid secret format - must be base64 encoded", nameof(secret), ex);
        }
    }

    /// <summary>
    /// Verifies and deserializes a webhook payload.
    /// </summary>
    /// <typeparam name="T">Type to deserialize payload into</typeparam>
    /// <param name="payload">Raw webhook payload string</param>
    /// <param name="headers">HTTP headers from the webhook request</param>
    /// <param name="tolerance">Maximum age of the webhook (default: 5 minutes)</param>
    /// <returns>Deserialized payload</returns>
    /// <exception cref="WebhookVerificationException">Thrown if verification fails</exception>
    public T Verify<T>(string payload, IDictionary<string, string> headers, TimeSpan? tolerance = null)
    {
        tolerance ??= TimeSpan.FromMinutes(5);

        // Extract required headers (case-insensitive)
        if (!TryGetHeader(headers, "webhook-id", out var webhookId))
        {
            throw new WebhookVerificationException("Missing webhook-id header");
        }

        if (!TryGetHeader(headers, "webhook-timestamp", out var timestampStr))
        {
            throw new WebhookVerificationException("Missing webhook-timestamp header");
        }

        if (!TryGetHeader(headers, "webhook-signature", out var signatureHeader))
        {
            throw new WebhookVerificationException("Missing webhook-signature header");
        }

        // Parse and validate timestamp
        if (!long.TryParse(timestampStr, out var timestamp))
        {
            throw new WebhookVerificationException("Invalid webhook-timestamp format");
        }

        var webhookTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        var now = DateTimeOffset.UtcNow;

        if (now - webhookTime > tolerance.Value)
        {
            throw new WebhookVerificationException(
                $"Webhook timestamp too old: {webhookTime:O} (now: {now:O}, tolerance: {tolerance.Value})"
            );
        }

        if (webhookTime - now > tolerance.Value)
        {
            throw new WebhookVerificationException(
                $"Webhook timestamp is in the future: {webhookTime:O} (now: {now:O}, tolerance: {tolerance.Value})"
            );
        }

        // Verify signature
        var version = _publicKey != null ? "v1a" : "v1";
        var expectedSignatures = ParseSignatures(signatureHeader, version);
        if (expectedSignatures.Count == 0)
        {
            throw new WebhookVerificationException(_publicKey != null
                ? "No v1a (Ed25519) signatures found in webhook-signature header"
                : "No signatures found in webhook-signature header");
        }

        // Sign the timestamp exactly as sent, as the sender did.
        var signedContent = $"{webhookId}.{timestampStr}.{payload}";

        var isValid = _publicKey != null
            ? VerifyEd25519(_publicKey, Encoding.UTF8.GetBytes(signedContent), expectedSignatures)
            : VerifyHmac(signedContent, expectedSignatures);

        if (!isValid)
        {
            throw new WebhookVerificationException("Signature verification failed");
        }

        // Deserialize payload
        try
        {
            var result = JsonSerializer.Deserialize<T>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                throw new WebhookVerificationException("Failed to deserialize payload: result is null");
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new WebhookVerificationException($"Failed to deserialize payload: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Verifies a webhook signature without deserializing.
    /// </summary>
    public bool VerifySignature(string payload, IDictionary<string, string> headers, TimeSpan? tolerance = null)
    {
        try
        {
            Verify<Dictionary<string, object>>(payload, headers, tolerance);
            return true;
        }
        catch (WebhookVerificationException)
        {
            return false;
        }
    }

    private bool VerifyHmac(string signedContent, List<string> signatures)
    {
        using var hmac = new HMACSHA256(_secretKey!);
        var computed = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent)));

        foreach (var signature in signatures)
        {
            if (SecureCompare(computed, signature))
            {
                return true;
            }
        }

        return false;
    }

    private static bool VerifyEd25519(Ed25519PublicKeyParameters publicKey, byte[] signedContent, List<string> signatures)
    {
        foreach (var signature in signatures)
        {
            byte[] signatureBytes;
            try
            {
                signatureBytes = Convert.FromBase64String(signature);
            }
            catch (FormatException)
            {
                continue;
            }

            if (signatureBytes.Length != Ed25519PublicKeyParameters.KeySize * 2)
            {
                continue;
            }

            // Signers are stateful, so one per check keeps the verifier thread-safe.
            var signer = new Ed25519Signer();
            signer.Init(false, publicKey);
            signer.BlockUpdate(signedContent, 0, signedContent.Length);
            if (signer.VerifySignature(signatureBytes))
            {
                return true;
            }
        }

        return false;
    }

    private static Ed25519PublicKeyParameters DecodePublicKey(string base64)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Invalid public key format - 'whpk_' must be followed by base64", "secret", ex);
        }

        if (bytes.Length != Ed25519PublicKeyParameters.KeySize)
        {
            throw new ArgumentException(
                $"Invalid public key - an Ed25519 public key is {Ed25519PublicKeyParameters.KeySize} bytes, got {bytes.Length}", "secret");
        }

        try
        {
            return new Ed25519PublicKeyParameters(bytes);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException("Invalid public key - not a valid Ed25519 point", "secret", ex);
        }
    }

    /// <summary>
    /// Parses a Standard Webhooks signature header: space-separated "version,signature" entries,
    /// e.g. "v1,abc= v1,def=" while a secret rotation is in its grace period. Only entries of
    /// <paramref name="version"/> ("v1" for HMAC, "v1a" for Ed25519) are returned. (This used to
    /// split on ',', which only worked for a single signature.)
    /// </summary>
    private static List<string> ParseSignatures(string signatureHeader, string version)
    {
        var signatures = new List<string>();
        var entries = signatureHeader.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        foreach (var entry in entries)
        {
            var comma = entry.IndexOf(',');
            if (comma <= 0 || comma == entry.Length - 1)
            {
                continue;
            }

            if (entry.Substring(0, comma) == version)
            {
                signatures.Add(entry.Substring(comma + 1));
            }
        }

        return signatures;
    }

    private static bool IsHex64(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SecureCompare(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);

        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private static bool TryGetHeader(IDictionary<string, string> headers, string key, out string value)
    {
        // Try exact match first
        if (headers.TryGetValue(key, out value!))
        {
            return true;
        }

        // Try case-insensitive match
        var matchingKey = headers.Keys.FirstOrDefault(k =>
            string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

        if (matchingKey != null)
        {
            value = headers[matchingKey];
            return true;
        }

        value = string.Empty;
        return false;
    }
}

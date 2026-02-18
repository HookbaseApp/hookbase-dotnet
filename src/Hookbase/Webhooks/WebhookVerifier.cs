using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hookbase.Exceptions;

namespace Hookbase.Webhooks;

/// <summary>
/// Verifies webhook signatures using HMAC-SHA256.
/// </summary>
public class WebhookVerifier
{
    private readonly byte[] _secretKey;
    private static readonly char[] Separator = new[] { ',' };

    /// <summary>
    /// Creates a new webhook verifier with the given secret.
    /// </summary>
    /// <param name="secret">Webhook signing secret (may include 'whsec_' prefix)</param>
    public WebhookVerifier(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret cannot be null or empty", nameof(secret));
        }

        // Remove 'whsec_' prefix if present
        var cleanSecret = secret.StartsWith("whsec_") ? secret.Substring(6) : secret;

        try
        {
            // Decode base64 secret
            _secretKey = Convert.FromBase64String(cleanSecret);
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

        // Verify signature
        var expectedSignatures = ParseSignatures(signatureHeader);
        if (expectedSignatures.Count == 0)
        {
            throw new WebhookVerificationException("No signatures found in webhook-signature header");
        }

        var signedContent = $"{webhookId}.{timestamp}.{payload}";
        var computedSignature = ComputeSignature(signedContent);

        var isValid = false;
        foreach (var expectedSig in expectedSignatures)
        {
            if (SecureCompare(computedSignature, expectedSig))
            {
                isValid = true;
                break;
            }
        }

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

    private string ComputeSignature(string signedContent)
    {
        using var hmac = new HMACSHA256(_secretKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
        return Convert.ToBase64String(hash);
    }

    private static List<string> ParseSignatures(string signatureHeader)
    {
        var signatures = new List<string>();
        var parts = signatureHeader.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("v1,"))
            {
                signatures.Add(trimmed.Substring(3));
            }
            else if (!trimmed.Contains(","))
            {
                // Assume it's a raw signature
                signatures.Add(trimmed);
            }
        }

        return signatures;
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

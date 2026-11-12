using System.Text.Json;
using Hookbase.Json;
using Hookbase.Models.Destinations;
using Hookbase.Models.Endpoints;
using Hookbase.Models.Sources;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Records print every member in ToString(), which ends up in logs and exception messages. The ones
/// that carry a secret print it as [redacted] and keep the rest.
/// </summary>
public class SecretRedactionTests
{
    private static int Count(string text, string part) =>
        (text.Length - text.Replace(part, "").Length) / part.Length;

    [Fact]
    public void Destination_hides_its_signing_secret()
    {
        var text = new Destination { Id = "dst_1", Name = "Orders", HasSigningSecret = true, SigningSecret = "whsec_dest" }.ToString();

        Assert.DoesNotContain("whsec_dest", text);
        Assert.StartsWith("Destination { Id = dst_1, ", text);
        Assert.Contains("Name = Orders", text);
        Assert.Contains("HasSigningSecret = True", text);
        Assert.Contains("SigningSecret = [redacted]", text);

        // An unset secret prints empty, as before, so the output still shows there was none.
        Assert.DoesNotContain("[redacted]", new Destination { Id = "dst_2" }.ToString());
    }

    [Fact]
    public void DestinationSecret_hides_the_secret_and_keeps_the_grace_period()
    {
        var text = new DestinationSecret("whsec_current", "2026-11-12T10:00:00Z").ToString();

        Assert.DoesNotContain("whsec_current", text);
        Assert.Equal("DestinationSecret { SigningSecret = [redacted], PreviousSecretExpiresAt = 2026-11-12T10:00:00Z }", text);
    }

    [Fact]
    public void Endpoint_and_EndpointWithSecret_hide_both_secrets_and_print_each_member_once()
    {
        Assert.DoesNotContain("whsec_read", new Endpoint { Id = "ep_1", Secret = "whsec_read" }.ToString());

        // EndpointWithSecret hides Endpoint.Secret with its own; set both.
        var created = (EndpointWithSecret)((Endpoint)new EndpointWithSecret
        {
            Id = "ep_1",
            Url = "https://a.example/hook",
            Secret = "whsec_new"
        } with { Secret = "whsec_base" });
        var text = created.ToString();

        Assert.DoesNotContain("whsec_new", text);
        Assert.DoesNotContain("whsec_base", text);
        Assert.StartsWith("EndpointWithSecret { Id = ep_1, ", text);
        Assert.Equal(2, Count(text, "Secret = [redacted]"));
        Assert.Equal(1, Count(text, "https://a.example/hook"));
    }

    [Fact]
    public void Source_and_SourceWithSecret_hide_the_secret_and_the_verify_token()
    {
        Assert.DoesNotContain("whsec_src", new Source { Id = "src_1", SigningSecret = "whsec_src" }.ToString());

        var created = JsonSerializer.Deserialize<SourceWithSecret>(
            """{"id":"src_1","name":"WhatsApp","signingSecret":"whsec_src","verifyToken":"vt_secret"}""",
            HookbaseJson.Options)!;
        Assert.Equal("whsec_src", created.SigningSecret);
        var text = created.ToString();

        Assert.DoesNotContain("whsec_src", text);
        Assert.DoesNotContain("vt_secret", text);
        Assert.StartsWith("SourceWithSecret { Id = src_1, ", text);
        Assert.Equal(1, Count(text, "Name = WhatsApp"));
        Assert.Contains("VerifyToken = [redacted]", text);
    }

    [Fact]
    public void RotateSecretResult_hides_the_new_secret()
    {
        var text = new RotateSecretResult
        {
            SignatureScheme = "hmac-sha256",
            Secret = "whsec_rotated",
            PreviousSecretExpiresAt = "2026-11-12T10:00:00Z",
            SecretVersion = 2
        }.ToString();

        Assert.DoesNotContain("whsec_rotated", text);
        Assert.Contains("Secret = [redacted]", text);
        Assert.Contains("SignatureScheme = hmac-sha256", text);
        Assert.Contains("SecretVersion = 2", text);
    }

    [Fact]
    public void Redaction_leaves_equality_alone()
    {
        Assert.Equal(new DestinationSecret("a", null), new DestinationSecret("a", null));
        Assert.NotEqual(new DestinationSecret("a", null), new DestinationSecret("b", null));
    }
}

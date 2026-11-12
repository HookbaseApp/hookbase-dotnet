using System.Text.Json;
using Hookbase.Json;
using Hookbase.Models.Destinations;
using Hookbase.Models.Endpoints;
using Hookbase.Models.PortalTokens;
using Hookbase.Models.Sources;
using Hookbase.Models.Tunnels;
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
    public void Source_requests_hide_the_secret_and_the_verify_token()
    {
        var create = new CreateSourceRequest
        {
            Name = "WhatsApp",
            Slug = "whatsapp",
            SigningSecret = "app_secret_create",
            VerifyToken = "vt_create"
        }.ToString();
        Assert.DoesNotContain("app_secret_create", create);
        Assert.DoesNotContain("vt_create", create);
        Assert.StartsWith("CreateSourceRequest { Name = WhatsApp, Slug = whatsapp, ", create);
        Assert.Contains("SigningSecret = [redacted]", create);
        Assert.Contains("VerifyToken = [redacted]", create);

        var update = new UpdateSourceRequest { Name = "Renamed", SigningSecret = "app_secret_update", VerifyToken = "vt_update" }.ToString();
        Assert.DoesNotContain("app_secret_update", update);
        Assert.DoesNotContain("vt_update", update);
        Assert.Contains("Name = Renamed", update);
        Assert.Equal(2, Count(update, "= [redacted]"));
    }

    [Fact]
    public void Tunnel_hides_its_auth_token_and_the_ws_url_that_carries_it()
    {
        var created = JsonSerializer.Deserialize<Tunnel>(
            """{"id":"tun_1","subdomain":"dev","auth_token":"tok_secret","wsUrl":"wss://api.example/tunnels/dev/ws?tunnelId=tun_1&token=tok_secret","tunnelUrl":"https://dev.example"}""",
            HookbaseJson.Options)!;
        Assert.Equal("tok_secret", created.AuthToken);
        var text = created.ToString();

        Assert.DoesNotContain("tok_secret", text);
        Assert.StartsWith("Tunnel { Id = tun_1, ", text);
        Assert.Contains("AuthToken = [redacted]", text);
        Assert.Contains("WsUrl = [redacted]", text);
        Assert.Contains("TunnelUrl = https://dev.example", text);
    }

    [Fact]
    public void Portal_and_pending_verification_tokens_are_hidden()
    {
        var portal = new PortalToken { Id = "pt_1", Token = "whpt_secret", TokenPrefix = "whpt_sec" }.ToString();
        Assert.DoesNotContain("whpt_secret", portal);
        Assert.Contains("Token = [redacted]", portal);
        Assert.Contains("TokenPrefix = whpt_sec", portal);

        var pending = new PendingVerification { Provider = "notion", Token = "secret_notion", ClientIp = "203.0.113.7" }.ToString();
        Assert.DoesNotContain("secret_notion", pending);
        Assert.Equal("PendingVerification { Provider = notion, Token = [redacted], ReceivedAt = , ClientIp = 203.0.113.7 }", pending);
    }

    [Fact]
    public void Storage_configs_hide_their_keys()
    {
        var s3 = new S3Config("bucket", "us-east-1", "AKIAEXAMPLE", "s3_secret_key").ToString();
        Assert.DoesNotContain("s3_secret_key", s3);
        Assert.Contains("Bucket = bucket", s3);
        Assert.Contains("SecretAccessKey = [redacted]", s3);

        var gcs = new GCSConfig("bucket", "proj", """{"private_key":"gcs_secret"}""").ToString();
        Assert.DoesNotContain("gcs_secret", gcs);
        Assert.Contains("ServiceAccountKey = [redacted]", gcs);

        var azure = new AzureBlobConfig("account", "azure_secret_key", "container").ToString();
        Assert.DoesNotContain("azure_secret_key", azure);
        Assert.Contains("AccountKey = [redacted]", azure);
        Assert.Contains("ContainerName = container", azure);

        // A request carrying one prints it through the config's own ToString().
        var request = new CreateDestinationRequest
        {
            Name = "Lake",
            Type = DestinationType.S3,
            Config = new S3Config("bucket", "us-east-1", "AKIAEXAMPLE", "s3_secret_key")
        }.ToString();
        Assert.DoesNotContain("s3_secret_key", request);
    }

    [Fact]
    public void Redaction_leaves_equality_alone()
    {
        Assert.Equal(new DestinationSecret("a", null), new DestinationSecret("a", null));
        Assert.NotEqual(new DestinationSecret("a", null), new DestinationSecret("b", null));
    }
}

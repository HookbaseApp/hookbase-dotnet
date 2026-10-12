using Hookbase.Exceptions;
using Hookbase.Models.Common;
using System.Net;
using System.Text;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// The public provider catalog: list summary (with and without the signatureVerification block),
/// full detail including the verify-token hint, and events.
/// </summary>
public class ProvidersTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public string? LastQuery { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastQuery = request.RequestUri!.Query;
            var (status, body) = request.RequestUri.AbsolutePath switch
            {
                "/api/catalog/providers" => (HttpStatusCode.OK, """
                    {"providers":[
                      {"id":"mollie-nextgen","name":"Mollie","category":"payments","description":"d","icon":"x",
                       "eventTypeCount":3,"hookbaseProvider":"mollie","hasSignatureVerification":true,"hasIpRanges":false,
                       "signatureVerification":{"supported":true,"kind":"hmac","secretSource":"provider","secretLabel":"Signing secret","secretPlaceholder":"whsec_..."}},
                      {"id":"old","name":"Old","category":"payments","description":"d","icon":"x","eventTypeCount":0,
                       "hookbaseProvider":"custom","hasSignatureVerification":false,"hasIpRanges":false}],
                     "categories":["auth","payments"],"total":2}
                    """),
                "/api/catalog/providers/whatsapp" => (HttpStatusCode.OK, """
                    {"provider":{"id":"whatsapp","name":"WhatsApp","hookbaseProvider":"whatsapp",
                     "signatureVerification":{"supported":true,"algorithm":"HMAC-SHA256","headerName":"X-Hub-Signature-256",
                       "secretLocationHint":"h","kind":"hmac","secretLabel":"App secret","secretSource":"provider",
                       "secretInstruction":"i","verifyToken":{"label":"Verify token","instruction":"paste it"}},
                     "eventTypes":[{"type":"messages","description":"m"}],"samplePayloads":{"messages":{"a":1}},
                     "ipRanges":null,"setupGuide":["one"]}}
                    """),
                "/api/catalog/providers/notion" => (HttpStatusCode.OK, """
                    {"provider":{"id":"notion","hookbaseProvider":"notion","signatureVerification":
                      {"supported":true,"kind":"hmac","secretSource":"provider","secretLabel":"Verification token","secretInstruction":"i","secretAfterRegistration":true}}}
                    """),
                "/api/catalog/providers/whatsapp/events" => (HttpStatusCode.OK,
                    """{"providerId":"whatsapp","providerName":"WhatsApp","eventTypes":[{"type":"messages","description":"m"}],"samplePayloads":{}}"""),
                _ => (HttpStatusCode.NotFound, """{"error":"Provider not found"}""")
            };
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static (HookbaseClient, StubHandler) Client()
    {
        var handler = new StubHandler();
        return (new HookbaseClient(new HookbaseClientOptions
        {
            ApiKey = "whr_test",
            BaseUrl = "https://api.test",
            MaxRetries = 0,
            HttpClient = new HttpClient(handler)
        }), handler);
    }

    [Fact]
    public async Task List_PassesFilters_AndDecodesSummary()
    {
        var (client, handler) = Client();

        var list = await client.Providers.ListAsync(category: "payments", search: "mol");

        Assert.Contains("category=payments", handler.LastQuery);
        Assert.Contains("search=mol", handler.LastQuery);
        Assert.Equal(2, list.Total);
        Assert.Equal(new[] { "auth", "payments" }, list.Categories);

        var mollie = list.Providers[0];
        Assert.Equal("mollie-nextgen", mollie.Id);
        Assert.Equal(SourceProvider.Mollie, mollie.HookbaseProvider);
        Assert.Equal(SecretSource.Provider, mollie.SignatureVerification!.SecretSource);
        Assert.Equal("hmac", mollie.SignatureVerification.Kind);
        Assert.False(mollie.SignatureVerification.SecretAfterRegistration);

        // An API without the list summary, or an unsigned provider, leaves it null.
        Assert.Null(list.Providers[1].SignatureVerification);
    }

    [Fact]
    public async Task Get_DecodesFullDetail()
    {
        var (client, _) = Client();

        var wa = await client.Providers.GetAsync("whatsapp");
        Assert.Equal("Verify token", wa.SignatureVerification!.VerifyToken!.Label);
        Assert.Equal("X-Hub-Signature-256", wa.SignatureVerification.HeaderName);
        Assert.Null(wa.IpRanges);
        Assert.Single(wa.EventTypes);
        Assert.Equal(1, wa.SamplePayloads["messages"].GetProperty("a").GetInt32());

        var notion = await client.Providers.GetAsync("notion");
        Assert.True(notion.SignatureVerification!.SecretAfterRegistration);
    }

    [Fact]
    public async Task Events_AndUnknownProvider()
    {
        var (client, _) = Client();

        var events = await client.Providers.GetEventsAsync("whatsapp");
        Assert.Equal("whatsapp", events.ProviderId);
        Assert.Single(events.EventTypes);

        await Assert.ThrowsAsync<NotFoundException>(() => client.Providers.GetAsync("nope"));
    }
}

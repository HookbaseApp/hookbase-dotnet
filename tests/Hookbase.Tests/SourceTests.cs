using Hookbase.Exceptions;
using Hookbase.Json;
using Hookbase.Models.Common;
using Hookbase.Models.Sources;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// The 2026-10 source contract: per-provider secrets (null on create for built-in and
/// secret-after-registration providers), the Meta verify token, pending verification tokens,
/// bulk delete, and the explicit nulls <see cref="UpdateSourceRequest.Clear"/> sends.
/// </summary>
public class SourceTests
{
    /// <summary>Answers each request from a table keyed by "METHOD path", recording what was sent.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _routes;

        public StubHandler(Dictionary<string, (HttpStatusCode, string)> routes) => _routes = routes;

        public List<(string Route, string? Query, string? Body, string? UserAgent)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var route = $"{request.Method} {request.RequestUri!.AbsolutePath}";
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((route, request.RequestUri.Query, body, request.Headers.UserAgent.ToString()));

            var (status, responseBody) = _routes.TryGetValue(route, out var hit)
                ? hit
                : (HttpStatusCode.InternalServerError, """{"error":"unexpected route"}""");
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }

    private static (HookbaseClient Client, StubHandler Handler) ClientFor(Dictionary<string, (HttpStatusCode, string)> routes)
    {
        var handler = new StubHandler(routes);
        var client = new HookbaseClient(new HookbaseClientOptions
        {
            ApiKey = "whr_test",
            BaseUrl = "https://api.test",
            MaxRetries = 0,
            HttpClient = new HttpClient(handler)
        });
        return (client, handler);
    }

    private static JsonObject Serialize<T>(T value)
        => JsonNode.Parse(JsonSerializer.Serialize(value, HookbaseJson.Options))!.AsObject();

    [Fact]
    public async Task CreateWise_SendsNoSecret_AndDecodesNullSecretAndVerifiesWithoutSecret()
    {
        var (client, handler) = ClientFor(new()
        {
            ["POST /api/sources"] = (HttpStatusCode.Created, """
                {"source":{"id":"src_1","name":"Wise","slug":"wise","provider":"wise","hasSigningSecret":false,
                 "signingSecret":null,"signingSecretLast4":null,"verifiesWithoutSecret":true,"awaitingSigningSecret":false}}
                """)
        });

        var source = await client.Sources.CreateAsync(new CreateSourceRequest
        {
            Name = "Wise",
            Slug = "wise",
            Provider = SourceProvider.Wise
        });

        var sent = JsonNode.Parse(handler.Requests.Single().Body!)!.AsObject();
        Assert.False(sent.ContainsKey("signingSecret"));
        Assert.Null(source.SigningSecret);
        Assert.True(source.VerifiesWithoutSecret);
        Assert.False(source.AwaitingSigningSecret);
    }

    [Fact]
    public void Source_DecodesAwaitingSigningSecretAndCounts()
    {
        var source = JsonSerializer.Deserialize<Source>(
            """{"id":"s","provider":"notion","awaitingSigningSecret":true,"verifiesWithoutSecret":0,"eventCount":12,"routeCount":3}""",
            HookbaseJson.Options)!;

        Assert.True(source.AwaitingSigningSecret);
        Assert.False(source.VerifiesWithoutSecret);
        Assert.Equal(12, source.EventCount);
        Assert.Equal(3, source.RouteCount);
    }

    [Fact]
    public async Task CreateWhatsApp_SendsAndReturnsVerifyToken()
    {
        var (client, handler) = ClientFor(new()
        {
            ["POST /api/sources"] = (HttpStatusCode.Created,
                """{"source":{"id":"src_1","provider":"whatsapp","signingSecret":"app-secret","verifyToken":"my-token"}}""")
        });

        var source = await client.Sources.CreateAsync(new CreateSourceRequest
        {
            Name = "WA",
            Slug = "wa",
            Provider = SourceProvider.Whatsapp,
            VerifyToken = "my-token"
        });

        var sent = JsonNode.Parse(handler.Requests.Single().Body!)!.AsObject();
        Assert.Equal("my-token", (string?)sent["verifyToken"]);
        Assert.Equal("whatsapp", (string?)sent["provider"]);
        Assert.Equal("my-token", source.VerifyToken);
        Assert.Equal("app-secret", source.SigningSecret);
    }

    [Fact]
    public void UpdateRequest_WithoutClear_OmitsNulls()
    {
        var body = Serialize(new UpdateSourceRequest { Name = "renamed", VerifyToken = "t" });

        Assert.Equal(new[] { "name", "verifyToken" }, body.Select(kvp => kvp.Key).OrderBy(k => k));
    }

    [Fact]
    public void UpdateRequest_Clear_SendsExplicitNulls()
    {
        var body = Serialize(new UpdateSourceRequest
        {
            Name = "renamed",
            Clear = { SourceField.SigningSecret, SourceField.VerifyToken, SourceField.AllowedMethods }
        });

        Assert.Equal(4, body.Count);
        Assert.Equal("renamed", (string?)body["name"]);
        foreach (var key in new[] { "signingSecret", "verifyToken", "allowedMethods" })
        {
            Assert.True(body.ContainsKey(key), key);
            Assert.Null(body[key]);
        }
    }

    [Fact]
    public void UpdateRequest_ClearingAFieldAlsoSet_Throws()
    {
        var request = new UpdateSourceRequest { SigningSecret = "x", Clear = { SourceField.SigningSecret } };

        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(request, HookbaseJson.Options));
    }

    [Fact]
    public void UpdateRequest_EverySourceFieldIsClearable()
    {
        // SourceField is meant to be exactly the converter's allowlist; a member without a wire key
        // would throw here instead of in a caller's update.
        foreach (var field in Enum.GetValues<SourceField>())
        {
            var body = Serialize(new UpdateSourceRequest { Clear = { field } });
            Assert.Single(body);
        }
    }

    [Fact]
    public async Task Update_PostsClearNullsOnTheWire()
    {
        var (client, handler) = ClientFor(new()
        {
            ["PATCH /api/sources/src_1"] = (HttpStatusCode.OK, """{"success":true}""")
        });

        await client.Sources.UpdateAsync("src_1", new UpdateSourceRequest { Clear = { SourceField.VerifyToken } });

        Assert.Equal("""{"verifyToken":null}""", handler.Requests.Single().Body);
    }

    [Fact]
    public async Task VerifyTokenEndpoints()
    {
        var (client, _) = ClientFor(new()
        {
            ["GET /api/sources/wa/verify-token"] = (HttpStatusCode.OK, """{"verifyToken":"current"}"""),
            ["POST /api/sources/wa/rotate-verify-token"] = (HttpStatusCode.OK, """{"verifyToken":"fresh"}""")
        });

        Assert.Equal("current", await client.Sources.GetVerifyTokenAsync("wa"));
        Assert.Equal("fresh", await client.Sources.RotateVerifyTokenAsync("wa"));
    }

    [Fact]
    public async Task PendingVerification_GetAndAdopt_AndNotFoundWhenNoneWaiting()
    {
        var (client, handler) = ClientFor(new()
        {
            ["GET /api/sources/notion/pending-verification"] = (HttpStatusCode.OK,
                """{"provider":"notion","token":"secret_abc","receivedAt":"2026-10-11T00:00:00Z","clientIp":null}"""),
            ["POST /api/sources/notion/pending-verification/adopt"] = (HttpStatusCode.OK, """{"success":true}"""),
            ["GET /api/sources/empty/pending-verification"] = (HttpStatusCode.NotFound,
                """{"error":"No verification token is waiting for this source"}"""),
            ["POST /api/sources/empty/pending-verification/adopt"] = (HttpStatusCode.NotFound,
                """{"error":"No verification token is waiting for this source"}""")
        });

        var pending = await client.Sources.GetPendingVerificationAsync("notion");
        Assert.Equal("notion", pending.Provider);
        Assert.Equal("secret_abc", pending.Token);
        Assert.Null(pending.ClientIp);

        await client.Sources.AdoptPendingVerificationAsync("notion");
        Assert.Contains(handler.Requests, r => r.Route == "POST /api/sources/notion/pending-verification/adopt");

        await Assert.ThrowsAsync<NotFoundException>(() => client.Sources.GetPendingVerificationAsync("empty"));
        await Assert.ThrowsAsync<NotFoundException>(() => client.Sources.AdoptPendingVerificationAsync("empty"));
    }

    [Fact]
    public async Task RotateSecret_RefusedForBuiltInProvider_IsValidationException()
    {
        var (client, _) = ClientFor(new()
        {
            ["POST /api/sources/wise/rotate-secret"] = (HttpStatusCode.BadRequest,
                """{"error":"wise signs with a public key it publishes, so this source has no secret to rotate."}""")
        });

        var ex = await Assert.ThrowsAsync<ValidationException>(() => client.Sources.RotateSecretAsync("wise"));
        Assert.Contains("no secret to rotate", ex.Message);
    }

    [Fact]
    public async Task BulkDelete_SendsIdsBody_AndReturnsCount()
    {
        var (client, handler) = ClientFor(new()
        {
            ["DELETE /api/sources/bulk"] = (HttpStatusCode.OK, """{"success":true,"deleted":2}""")
        });

        var deleted = await client.Sources.BulkDeleteAsync(new[] { "src_1", "src_2" });

        Assert.Equal(2, deleted);
        Assert.Equal("""{"ids":["src_1","src_2"]}""", handler.Requests.Single().Body);
    }

    [Fact]
    public async Task EventsList_UnknownSourceSlug_IsNotFound()
    {
        var (client, handler) = ClientFor(new()
        {
            ["GET /api/events"] = (HttpStatusCode.NotFound, """{"error":"Source not found"}""")
        });

        await Assert.ThrowsAsync<NotFoundException>(() => client.Events.ListAsync(sourceId: "my-slug"));
        Assert.Contains("sourceId=my-slug", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task UserAgent_ReportsThePackageVersion()
    {
        var (client, handler) = ClientFor(new()
        {
            ["GET /api/sources/s"] = (HttpStatusCode.OK, """{"source":{"id":"s"}}""")
        });

        await client.Sources.GetAsync("s");

        var version = typeof(HookbaseClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        Assert.Equal($"Hookbase.NET/{version}", handler.Requests.Single().UserAgent);
        Assert.NotEqual("1.0.0", version);
    }

    [Fact]
    public void SourceProvider_ListsEveryAcceptedProvider()
    {
        var values = typeof(SourceProvider)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetValue(null)!)
            .ToList();

        Assert.Equal(78, values.Count);
        Assert.Equal(values.Count, values.Distinct().Count());
        Assert.Contains("wise", values);
        Assert.Contains("whatsapp", values);
        Assert.Contains("sendgrid", values);
    }
}

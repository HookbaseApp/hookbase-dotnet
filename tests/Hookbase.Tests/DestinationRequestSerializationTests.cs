using Hookbase.Exceptions;
using Hookbase.Json;
using Hookbase.Models.Destinations;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Hookbase.Tests;

/// <summary>
/// Wire-format tests for POST /api/destinations. The API requires a slug matching
/// <c>^[a-z0-9-]+$</c> (max 50 characters), so the SDK derives one when the caller omits it.
/// The derivation has to stay byte-identical to the other Hookbase SDKs - the reference is
/// <c>deriveDestinationSlug</c> in <c>node-sdk/src/resources/wire.ts</c>.
/// </summary>
public class DestinationRequestSerializationTests
{
    private const string SlugPattern = "^[a-z0-9-]+$";

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, HookbaseJson.Options);

    private static JsonElement Parse<T>(T value)
        => JsonDocument.Parse(Serialize(value)).RootElement.Clone();

    [Fact]
    public void Create_WithoutASlug_SerializesASlugDerivedFromTheName()
    {
        var request = new CreateDestinationRequest { Name = "Billing Service (EU)" };

        var slug = Parse(request).GetProperty("slug").GetString();

        Assert.Equal("billing-service-eu", slug);
        Assert.Matches(SlugPattern, slug!);
        Assert.Equal("{\"name\":\"Billing Service (EU)\",\"slug\":\"billing-service-eu\"}", Serialize(request));
    }

    [Fact]
    public void Create_WithAnExplicitSlug_SendsItUnchanged()
    {
        var request = new CreateDestinationRequest { Name = "Billing Service (EU)", Slug = "billing-eu" };

        Assert.Equal("billing-eu", Parse(request).GetProperty("slug").GetString());
        Assert.Equal("billing-eu", request.WithResolvedSlug().Slug);
    }

    [Fact]
    public void Create_WithABlankSlug_FallsBackToTheDerivedSlug()
    {
        var request = new CreateDestinationRequest { Name = "Billing Service", Slug = "   " };

        Assert.Equal("billing-service", Parse(request).GetProperty("slug").GetString());
    }

    [Theory]
    [InlineData("Billing Service (EU)", "billing-service-eu")]
    [InlineData("  Leading & trailing  ", "leading-trailing")]
    [InlineData("Multiple---Hyphens!!!Collapse", "multiple-hyphens-collapse")]
    [InlineData("ALL CAPS 42", "all-caps-42")]
    [InlineData("already-a-slug", "already-a-slug")]
    [InlineData("Stripe → Prod", "stripe-prod")]
    [InlineData("s3: cold storage", "s3-cold-storage")]
    [InlineData("42", "42")]
    public void DeriveSlug_ProducesAnApiAcceptableSlug(string name, string expected)
    {
        var slug = CreateDestinationRequest.DeriveSlug(name);

        Assert.Equal(expected, slug);
        Assert.Matches(SlugPattern, slug);
        Assert.True(slug.Length <= 50);
    }

    // Accents: the decomposition is dropped so the letter survives as a letter.
    [Theory]
    [InlineData("Café EU", "cafe-eu")]            // precomposed é
    [InlineData("Café EU", "cafe-eu")]           // e + combining acute
    [InlineData("Ångström Ingest", "angstrom-ingest")]
    [InlineData("naïve résumé", "naive-resume")]
    public void DeriveSlug_FoldsAccentsInsteadOfDroppingTheLetter(string name, string expected)
        => Assert.Equal(expected, CreateDestinationRequest.DeriveSlug(name));

    // NFKD, not NFD: compatibility forms fold too, which is what the reference implementation does.
    [Theory]
    [InlineData("½ cup", "1-2-cup")]
    [InlineData("ＡＢＣ", "abc")]
    public void DeriveSlug_FoldsCompatibilityFormsLikeTheReferenceImplementation(string name, string expected)
        => Assert.Equal(expected, CreateDestinationRequest.DeriveSlug(name));

    // Truncation is a plain 50-character cut that may land mid-word; only a hyphen the cut left
    // behind is trimmed. Word-wrapping here would drift from the other SDKs.
    [Theory]
    [InlineData(
        "A destination name that is really quite a lot longer than fifty characters",
        "a-destination-name-that-is-really-quite-a-lot-long")]
    [InlineData(
        "Destination name that is well past the fifty character limit",
        "destination-name-that-is-well-past-the-fifty-chara")]
    public void DeriveSlug_TruncatesWithAPlainFiftyCharacterCut(string name, string expected)
    {
        var slug = CreateDestinationRequest.DeriveSlug(name);

        Assert.Equal(expected, slug);
        Assert.Equal(50, slug.Length);
        Assert.Matches(SlugPattern, slug);
    }

    [Fact]
    public void DeriveSlug_TrimsAHyphenLeftBehindByTheCut()
    {
        // The 51st character is the separator, so the cut leaves a trailing hyphen to trim.
        var name = new string('a', 50) + " tail";

        var slug = CreateDestinationRequest.DeriveSlug(name);

        Assert.Equal(new string('a', 50), slug);
        Assert.Matches(SlugPattern, slug);
    }

    // Lowercasing must be invariant: ToLower() under tr-TR turns 'I' into 'ı', which is not in
    // ^[a-z0-9-]+$ and would make this machine derive a different slug from every other machine.
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-AZ")]
    [InlineData("lt-LT")]
    public void DeriveSlug_IgnoresTheCurrentCulture(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            Assert.Equal("izmir-ingest", CreateDestinationRequest.DeriveSlug("IZMIR Ingest"));
            Assert.Equal("izmir-ingest", CreateDestinationRequest.DeriveSlug("İZMIR Ingest"));
            Assert.Equal("i", CreateDestinationRequest.DeriveSlug("I"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("☃☃☃")]
    public void DeriveSlug_ThrowsWhenThereIsNothingToDeriveFrom(string? name)
    {
        var exception = Assert.Throws<HookbaseException>(() => CreateDestinationRequest.DeriveSlug(name));

        Assert.Contains("Cannot derive a destination slug", exception.Message);
        Assert.Contains("^[a-z0-9-]+$", exception.Message);
        Assert.Contains("Slug explicitly", exception.Message);
    }

    [Fact]
    public void WithResolvedSlug_ThrowsWhenTheNameHasNothingToDeriveFrom()
    {
        var request = new CreateDestinationRequest { Name = "☃☃☃" };

        Assert.Null(request.Slug);
        Assert.Throws<HookbaseException>(() => request.WithResolvedSlug());
    }

    [Fact]
    public void WithResolvedSlug_FillsInTheDerivedSlug()
    {
        var request = new CreateDestinationRequest { Name = "Billing Service (EU)" };

        Assert.Equal("billing-service-eu", request.WithResolvedSlug().Slug);
    }

    /// <summary>
    /// The cross-SDK slug contract. Every Hookbase SDK carries this table verbatim, so a drift in
    /// any one implementation fails a test here as well as there. Change one, change them all:
    /// <list type="bullet">
    /// <item><description><c>node-sdk/src/__tests__/wire-format.test.ts</c> (cross-SDK contract block)</description></item>
    /// <item><description><c>python-sdk/tests/test_slug_contract.py</c> (<c>CROSS_SDK_SLUG_CASES</c>)</description></item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData("Caf\u00e9 EU", "cafe-eu")]
    [InlineData("Acme Orders (EU)", "acme-orders-eu")]
    [InlineData("My Backend (EU)", "my-backend-eu")]
    [InlineData("  spaced  out  ", "spaced-out")]
    [InlineData("UPPER CASE", "upper-case")]
    [InlineData("\u00fcn\u00efc\u00f6d\u00e9 n\u00e4mes", "unicode-names")]
    [InlineData(
        "a-very-long-destination-name-that-runs-well-past-the-fifty-character-limit",
        "a-very-long-destination-name-that-runs-well-past-t")]
    [InlineData("trailing---hyphens---", "trailing-hyphens")]
    [InlineData("123 numeric", "123-numeric")]
    // \u00c6 and \u00d8 do not decompose under NFKD, so they are dropped rather than folded.
    // Agreed shared behaviour across the SDKs - do not "fix" it in .NET alone.
    [InlineData("\u00c6r\u00f8 \u00d8mega", "r-mega")]
    [InlineData("\ufb01le ligature", "file-ligature")]
    [InlineData("Mixed 123 ABC xyz", "mixed-123-abc-xyz")]
    [InlineData("don't stop", "don-t-stop")]
    [InlineData("a", "a")]
    // Combining marks outside the U+0300-U+036F block. These rows exist because Node and Python
    // stripped only that block and so produced "a-b" here, while this SDK drops the whole Mn
    // category and produced "ab". Both were widened to the category to agree with this.
    [InlineData("a\u064db", "ab")]
    [InlineData("\u0939\u093f\u0928\u094d\u0926\u0940 name", "name")]
    [InlineData("\u05d0\u05b8 hebrew", "hebrew")]
    public void CrossSdkSlugCases(string name, string expected)
    {
        var slug = CreateDestinationRequest.DeriveSlug(name);

        Assert.Equal(expected, slug);
        Assert.Matches(SlugPattern, slug);
        Assert.True(slug.Length <= 50);
    }

    /// <summary>
    /// Last row of the cross-SDK table: no alphanumerics to derive from, so every SDK raises its
    /// own error instead of sending an invalid slug.
    /// </summary>
    [Fact]
    public void CrossSdkSlugCases_ThrowsWhenThereAreNoAlphanumerics()
        => Assert.Throws<HookbaseException>(() => CreateDestinationRequest.DeriveSlug("\u2603\u2603\u2603"));

    [Fact]
    public void Create_UsesTimeoutMsAndNeverSendsTheOtherSdksLegacyKeys()
    {
        var request = new CreateDestinationRequest
        {
            Name = "Billing",
            Slug = "billing",
            Type = DestinationType.Http,
            Url = "https://billing.example.com/hooks",
            TimeoutMs = 5000
        };

        var body = Parse(request);

        Assert.Equal(5000, body.GetProperty("timeoutMs").GetInt32());
        Assert.False(body.TryGetProperty("timeout", out _));
        Assert.False(body.TryGetProperty("timeoutSeconds", out _));
        Assert.False(body.TryGetProperty("description", out _));
        Assert.False(body.TryGetProperty("retryCount", out _));
        Assert.False(body.TryGetProperty("retryInterval", out _));
    }

    [Fact]
    public void Import_SerializesADerivedSlugForEveryNestedDestination()
    {
        var request = new ImportDestinationsRequest
        {
            Destinations = new List<CreateDestinationRequest>
            {
                new() { Name = "Billing Service (EU)" },
                new() { Name = "Analytics", Slug = "analytics-v2" }
            }
        };

        var destinations = Parse(request).GetProperty("destinations");

        Assert.Equal("billing-service-eu", destinations[0].GetProperty("slug").GetString());
        Assert.Equal("analytics-v2", destinations[1].GetProperty("slug").GetString());
    }

    [Fact]
    public void Create_RecordEqualityStillSeesTheSlug()
    {
        var derived = new CreateDestinationRequest { Name = "Billing Service" };
        var explicitSlug = derived with { Slug = "billing" };

        Assert.NotEqual(derived, explicitSlug);
        Assert.Equal(derived, new CreateDestinationRequest { Name = "Billing Service" });
        Assert.Equal("billing", explicitSlug.Slug);
    }

    [Fact]
    public void DerivedSlugs_AlwaysSatisfyTheApiRegex()
    {
        var names = new[]
        {
            "Stripe → Prod", "s3: cold storage", "Café Orders", "42",
            "½ cup", "ＡＢＣ", "  spaced  out  ", "UPPER_snake-Case.42"
        };

        foreach (var name in names)
        {
            var slug = CreateDestinationRequest.DeriveSlug(name);

            Assert.True(Regex.IsMatch(slug, SlugPattern), $"'{name}' produced '{slug}'");
            Assert.True(slug.Length <= 50);
        }
    }
}

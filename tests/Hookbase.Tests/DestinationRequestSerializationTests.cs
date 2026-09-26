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
    /// <item><description><c>go-sdk/destinations_fields_test.go</c> (<c>crossSDKSlugCases</c>)</description></item>
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
    // The rows below are the cases where the four implementations can disagree for reasons that
    // have nothing to do with the algorithm, so they are the ones worth pinning. Each was run
    // against all four before being written down.
    //
    // Not marks themselves (both are Lm) but their NFKD is one, so decomposing before stripping
    // makes them vanish rather than separate. Go folds per rune and needed an explicit empty fold
    // to match.
    [InlineData("a\uff9eb", "ab")]
    [InlineData("a\uff9fb", "ab")]
    // Mn only from Unicode 16, so a runtime on 15.0 does not strip it without SlugMarkAdditions.
    [InlineData("a\u1acfb", "ab")]
    // The same, outside the BMP: this SDK read it as two surrogate halves, neither of them a mark,
    // until the fold moved to runes.
    [InlineData("a\U0001e5eeb", "ab")]
    // The other direction: Mn in Unicode 15.0 and Mc from 15.1. A spacing mark separates.
    [InlineData("a\U0001171eb", "a-b")]
    // Unassigned before Unicode 16, where it decomposes to "A". Without PreFold an older runtime
    // leaves it alone and it separates instead.
    [InlineData("a\U0001ccd6b", "aab")]
    // A noncharacter: separates, and must not throw. Normalize rejects these outright, which is why
    // PreFold swaps in U+FFFD.
    [InlineData("a\ufffeb", "a-b")]
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

    /// <summary>
    /// The 75 marks the reference runtime strips that Unicode 15.0 does not know, written out
    /// independently of <c>SlugMarkAdditions</c> so a typo in one of its ranges fails here.
    /// </summary>
    /// <remarks>
    /// .NET 8's own tables already classify 36 of these as non-spacing marks, so on this runtime
    /// that many rows pass whether or not the table is right; the exhaustive check lives in the
    /// Python and Go suites, whose runtimes are further behind. What this pins on every runtime is
    /// the other 39, and the rune-wise fold - a char loop saw the astral ones as surrogate halves.
    /// </remarks>
    private const string NewerUnicodeMarks =
        "0897 1ACF-1ADD 1AE0-1AEB 10D69-10D6D 10EFA-10EFC 113BB-113C0 113CE 113D0 113D2 " +
        "113E1-113E2 11B60 11B62-11B64 11B66 11F5A 1611E-16129 1612D-1612F 1E5EE-1E5EF 1E6E3 " +
        "1E6E6 1E6EE-1E6EF 1E6F5";

    private static IEnumerable<int> NewerUnicodeMarkCodePoints()
    {
        foreach (var span in NewerUnicodeMarks.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var bounds = span.Split('-');
            var low = int.Parse(bounds[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var high = int.Parse(bounds[^1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            for (var codePoint = low; codePoint <= high; codePoint++)
            {
                yield return codePoint;
            }
        }
    }

    [Fact]
    public void DeriveSlug_StripsMarksANewerUnicodeAdded()
    {
        var codePoints = NewerUnicodeMarkCodePoints().ToList();
        Assert.Equal(75, codePoints.Count);

        foreach (var codePoint in codePoints)
        {
            var name = "a" + char.ConvertFromUtf32(codePoint) + "b";
            Assert.Equal("ab", CreateDestinationRequest.DeriveSlug(name));
        }
    }

    [Fact]
    public void DeriveSlug_FoldsTheCharactersANewerUnicodeDecomposesToAscii()
    {
        // Unassigned before Unicode 16, so Normalize leaves them alone here and PreFold is what
        // makes them letters instead of separators.
        Assert.Equal("asb", CreateDestinationRequest.DeriveSlug("a\ua7f1b"));

        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        for (var offset = 0; offset < alphabet.Length; offset++)
        {
            var name = "a" + char.ConvertFromUtf32(0x1CCD6 + offset) + "b";
            Assert.Equal($"a{alphabet[offset]}b", CreateDestinationRequest.DeriveSlug(name));
        }
    }
}

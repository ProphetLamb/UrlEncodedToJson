using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using UrlEncodedToJson.Serialization;

namespace UrlEncodedToJson.Tests;

[TestFixture]
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
public sealed class JsonNumberTests
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    #region TryParse - deterministic valid cases

    [Test]
    public void TryParse_UsesCanonicalZeroRepresentation()
    {
        var zeroInputs = new[]
        {
            "0",
            "-0",
            "+0",
            "0.0",
            "-0.0",
            "+0.000",
            "0000",
            "0000.0000",
            "0e10",
            "0e-999999",
            "000.000e+123456789"
        };

        foreach (var text in zeroInputs)
        {
            var ok = JsonNumber.TryParse(text, out var value);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ok, Is.True, $"Expected TryParse to succeed for '{text}'.");
                Assert.That(ToDecimal(value), Is.Zero);
            }
        }
    }

    #endregion

    #region TryParse - invalid inputs

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t\r\n")]
    [TestCase("+")]
    [TestCase("-")]
    [TestCase(".")]
    [TestCase("+.")]
    [TestCase("-.")]
    [TestCase("e10")]
    [TestCase("E10")]
    [TestCase("1e")]
    [TestCase("1E")]
    [TestCase("1e+")]
    [TestCase("1e-")]
    [TestCase("1..2")]
    [TestCase("1.2.3")]
    [TestCase("1ee2")]
    [TestCase("1e2e3")]
    [TestCase("--1")]
    [TestCase("++1")]
    [TestCase("+-1")]
    [TestCase("-+1")]
    [TestCase("1-")]
    [TestCase("1+")]
    [TestCase("abc")]
    [TestCase("1a")]
    [TestCase("a1")]
    [TestCase("1_000")]
    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    [TestCase("0x10")]
    [TestCase("1,23")]
    [TestCase(" 1 2 ")]
    public void TryParse_InvalidInputs_ReturnFalse(string text)
    {
        var ok = JsonNumber.TryParse(text, out var value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ok, Is.False, $"Expected TryParse to fail for '{text}'.");
            Assert.That(value.ToString(), Is.Null);
        }
    }

    #endregion

    #region ToString - deterministic cases

    [Test]
    public void ToString_DefaultOverride_MatchesGeneralInvariantFormatting()
    {
        var text = "123.4500e-2";
        var value = ParseOrFail(text);

        Assert.That(value.ToString(), Is.EqualTo(text));
    }

    #endregion

    #region Json serialization / deserialization

    [TestCase("123")]
    [TestCase("-123")]
    [TestCase("123.45")]
    [TestCase("-123.45")]
    [TestCase("123e6")]
    [TestCase("123.45e-56")]
    [TestCase(".5", "0.5")]
    [TestCase("-.5", "-0.5")]
    public void JsonSerializer_Deserialize_NumberToken_Works(string input, string? expectedCanonical = null)
    {
        expectedCanonical ??= ParseOrFail(input).ToString();

        var json = expectedCanonical; // must be legal JSON numeric syntax

        var value = JsonSerializer.Deserialize<JsonNumber>(json ?? "", JsonOptions);

        Assert.That(value.ToString(), Is.EqualTo(expectedCanonical));
    }

    [TestCase("\"123\"")]
    [TestCase("\"-123\"")]
    [TestCase("\"123.45\"")]
    [TestCase("\"-123.45\"")]
    [TestCase("\"123e6\"")]
    [TestCase("\"123.45e-56\"")]
    [TestCase("\".5\"")]
    [TestCase("\"-.5\"")]
    public void JsonSerializer_Deserialize_StringToken_Works(string json)
    {
        var value = JsonSerializer.Deserialize<JsonNumber>(json, JsonOptions);

        var raw = JsonSerializer.Deserialize<string>(json, JsonOptions)!;
        var parsed = ParseOrFail(raw);

        Assert.That(parsed, Is.EqualTo(value));
    }

    [Test]
    public void JsonSerializer_Serialize_WritesJsonNumber_NotJsonString()
    {
        var value = ParseOrFail("123.45");

        var json = JsonSerializer.Serialize(value, JsonOptions);

        Assert.That(json, Is.EqualTo("123.45"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(json, Does.Not.StartWith("\""));
            Assert.That(json, Does.Not.EndWith("\""));
        }
    }

    [TestCase("123")]
    [TestCase("-123")]
    [TestCase("123.45")]
    [TestCase("-123.45e-56")]
    [TestCase("0")]
    [TestCase("0.000")]
    [TestCase("1000000000000000000000")]
    [TestCase("0.000000000000000000001")]
    public void JsonSerializer_RoundTrip_PreservesCanonicalValue(string input)
    {
        var original = ParseOrFail(input);

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<JsonNumber>(json, JsonOptions);

        Assert.That(original, Is.EqualTo(deserialized));
    }

    [TestCase("true")]
    [TestCase("false")]
    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("[]")]
    public void JsonSerializer_InvalidJsonInput_Throws(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<JsonNumber>(json, JsonOptions));
    }

    [TestCase("\"\"")]
    [TestCase("\" \"")]
    [TestCase("\"abc\"")]
    [TestCase("\"1e\"")]
    [TestCase("\".\"")]
    public void JsonSerializer_InvalidFormatInput_Throws(string json)
    {
        Assert.Throws<FormatException>(() => JsonSerializer.Deserialize<JsonNumber>(json, JsonOptions));
    }

    #endregion

    #region Fuzzing / randomized property tests

    [Test]
    public void Fuzz_RandomValidNumericStrings_ParseThenToStringThenParse_RoundTrips()
    {
        var rng = new Random(0x1BADB002);

        for (var iteration = 0; iteration < 5_000; iteration++)
        {
            var text = CreateRandomNumericLiteral(rng);

            var ok1 = JsonNumber.TryParse(text, out var first);
            Assert.That(ok1, Is.True, $"Expected generated numeric literal to parse: '{text}'");

            var canonical = first.ToString();

            var ok2 = JsonNumber.TryParse(canonical, out var second);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(ok2, Is.True, $"Expected canonical text to parse: '{canonical}' from '{text}'");
                Assert.That(second.ToString(), Is.EqualTo(canonical));
            }
        }
    }

    [Test]
    public void Fuzz_RandomGarbageInput_DoesNotThrowAndUsuallyFails()
    {
        var rng = new Random(0xC0FFEE);

        var sawFailure = false;

        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var text = CreateRandomGarbage(rng, rng.Next(0, 40));

            Assert.DoesNotThrow(() =>
            {
                var ok = JsonNumber.TryParse(text, out _);
                if (!ok)
                {
                    sawFailure = true;
                }
            }, $"TryParse should never throw for random garbage input '{text}'.");
        }

        Assert.That(sawFailure, Is.True, "Sanity check: expected at least one random garbage input to fail parsing.");
    }

    [Test]
    public void Fuzz_DecimalRepresentableValues_AgreeExactlyWithDecimal()
    {
        var rng = new Random(42);

        for (var iteration = 0; iteration < 10000; iteration++)
        {
            var value = CreateRandomDecimal(rng);

            // G29 is the conventional round-trip-ish format for decimal textual representation
            var text = value.ToString("G29", CultureInfo.InvariantCulture);

            var ok = JsonNumber.TryParse(text, out var parsed);
            Assert.That(ok, Is.True, $"Expected decimal text to parse: '{text}'");

            var reconstructed = ToDecimal(parsed);

            Assert.That(reconstructed, Is.EqualTo(value), $"Mismatch for decimal value '{text}'");
        }
    }

    #endregion

    #region Helpers

    private static JsonNumber ParseOrFail(string text)
    {
        var ok = JsonNumber.TryParse(text, out var value);
        Assert.That(ok, Is.True, $"Expected TryParse to succeed for '{text}'.");
        return value;
    }

    private static string CreateRandomNumericLiteral(Random rng)
    {
        var sb = new StringBuilder();

        // Optional sign
        var signPick = rng.Next(3);
        if (signPick == 1)
        {
            sb.Append('+');
        }

        if (signPick == 2)
        {
            sb.Append('-');
        }

        var form = rng.Next(4);
        switch (form)
        {
            case 0:
                // integer
                sb.Append(CreateRandomDigits(rng, rng.Next(1, 40), allowLeadingZero: true));
                break;

            case 1:
                // integer.fraction
                sb.Append(CreateRandomDigits(rng, rng.Next(1, 25), allowLeadingZero: true));
                sb.Append('.');
                sb.Append(CreateRandomDigits(rng, rng.Next(1, 25), allowLeadingZero: true));
                break;

            case 2:
                // .fraction
                sb.Append('.');
                sb.Append(CreateRandomDigits(rng, rng.Next(1, 25), allowLeadingZero: true));
                break;

            default:
                // integer.?
                sb.Append(CreateRandomDigits(rng, rng.Next(1, 25), allowLeadingZero: true));
                if (rng.Next(2) == 0)
                {
                    sb.Append('.');
                    if (rng.Next(2) == 0)
                    {
                        sb.Append(CreateRandomDigits(rng, rng.Next(1, 25), allowLeadingZero: true));
                    }
                }
                break;
        }

        // Optional exponent
        if (rng.Next(2) == 0)
        {
            sb.Append(rng.Next(2) == 0 ? 'e' : 'E');

            var expSignPick = rng.Next(3);
            if (expSignPick == 1)
            {
                sb.Append('+');
            }

            if (expSignPick == 2)
            {
                sb.Append('-');
            }

            sb.Append(CreateRandomDigits(rng, rng.Next(1, 8), allowLeadingZero: true));
        }

        // Optional trailing whitespace
        AppendRandomWhitespace(rng, sb, maxLength: 2);

        return sb.ToString();
    }

    private static string CreateRandomGarbage(Random rng, int length)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*_=,;:/?\\|~`'\"[]{}()<>\0\t\r\n ";
        var sb = new StringBuilder(length);

        for (var i = 0; i < length; i++)
        {
            sb.Append(alphabet[rng.Next(alphabet.Length)]);
        }

        return sb.ToString();
    }

    private static string CreateRandomDigits(Random rng, int length, bool allowLeadingZero)
    {
        if (length <= 0)
        {
            return string.Empty;
        }

        var chars = new char[length];

        chars[0] = allowLeadingZero
            ? (char)('0' + rng.Next(10))
            : (char)('1' + rng.Next(9));

        for (var i = 1; i < length; i++)
        {
            chars[i] = (char)('0' + rng.Next(10));
        }

        return new string(chars);
    }

    private static void AppendRandomWhitespace(Random rng, StringBuilder sb, int maxLength)
    {
        var count = rng.Next(maxLength + 1);
        for (var i = 0; i < count; i++)
        {
            sb.Append(rng.Next(4) switch
            {
                0 => ' ',
                1 => '\t',
                2 => '\r',
                _ => '\n'
            });
        }
    }

    private static decimal CreateRandomDecimal(Random rng)
    {
        // Build a decimal from mantissa / scale within decimal's representable range.
        // mantissa: up to 28-29 digits is okay for decimal
        var lo = rng.Next(int.MinValue, int.MaxValue);
        var mid = rng.Next(int.MinValue, int.MaxValue);
        var hi = rng.Next(0, int.MaxValue); // keep it positive-ish to avoid overflow likelihood
        var isNegative = rng.Next(2) == 0;
        var scale = (byte)rng.Next(0, 29);

        return new decimal(lo, mid, hi, isNegative, scale);
    }

    private static decimal ToDecimal(JsonNumber value)
    {
        return decimal.Parse(value.Text!,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
    }

    #endregion
}

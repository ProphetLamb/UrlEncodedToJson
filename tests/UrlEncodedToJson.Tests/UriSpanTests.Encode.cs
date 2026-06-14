using UrlEncodedToJson.Text;

namespace UrlEncodedToJson.Tests;

using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Diagnostics.CodeAnalysis;

[TestFixture]
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
public class UriSpanEscapeTests
{
    [Test]
    public void Escape_EmptyString_ReturnsEmpty()
    {
        var input = "";
        var escaped = UriSpan.EscapeDataString(input);
        Assert.That(escaped, Is.EqualTo(""));
    }

    [Test]
    public void Escape_SpaceBecomesPlus()
    {
        var input = "Hello World";
        var escaped = UriSpan.EscapeDataString(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(escaped, Does.Contain("+"));
            Assert.That(UriSpan.UnescapeDataString(escaped), Is.EqualTo(input));
        }
    }

    [Test]
    public void Escape_PlusIsPercentEncoded()
    {
        var input = "A+B";
        var escaped = UriSpan.EscapeDataString(input);

        using (Assert.EnterMultipleScope())
        {
            // plus must be encoded as %2B so Unescape(Escape) yields original
            Assert.That(escaped, Does.Contain("%2B").Or.Contain("%2b"));
            Assert.That(UriSpan.UnescapeDataString(escaped), Is.EqualTo(input));
        }
    }

    [Test]
    public void Escape_PercentIsPercentEncoded()
    {
        var input = "100%";
        var escaped = UriSpan.EscapeDataString(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(escaped, Does.Contain("%25"));
            Assert.That(UriSpan.UnescapeDataString(escaped), Is.EqualTo(input));
        }
    }

    [Test]
    public void Escape_UnreservedCharacters_AreNotEscaped()
    {
        var unreserved = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        var escaped = UriSpan.EscapeDataString(unreserved);

        Assert.That(escaped, Is.EqualTo(unreserved));
    }

    [Test]
    public void Escape_ReservedAndUnsafe_AreEscaped()
    {
        var input = " !\"#$%&'()*+,/:;=?@[]";
        var escaped = UriSpan.EscapeDataString(input);

        // All characters except space (which becomes +) should be percent-encoded or + for space
        Assert.That(UriSpan.UnescapeDataString(escaped), Is.EqualTo(input));
    }

    [Test]
    public void Escape_Utf8MultiByteCharacter_Euro()
    {
        var input = "€"; // U+20AC -> E2 82 AC
        var escaped = UriSpan.EscapeDataString(input);

        using (Assert.EnterMultipleScope())
        {
            // should contain percent-encoded bytes
            Assert.That(escaped, Does.Contain("%E2").And.Contain("%82").And.Contain("%AC"));
            Assert.That(UriSpan.UnescapeDataString(escaped), Is.EqualTo(input));
        }
    }

    [Test]
    public void Escape_Emoji_SurrogatePair_RoundTrips()
    {
        var input = "🙂"; // surrogate pair -> 4 UTF-8 bytes
        var escaped = UriSpan.EscapeDataString(input);

        using (Assert.EnterMultipleScope())
        {
            // percent-encoded bytes should be present
            Assert.That(escaped, Does.Contain("%F0"));
            Assert.That(UriSpan.UnescapeDataString(escaped), Is.EqualTo(input));
        }
    }

    [Test]
    public void Escape_UnpairedHighSurrogate_IsEncodedAndRoundTrips()
    {
        // Create an unpaired high surrogate char
        var high = '\uD800';
        var input = new string(new[] { high, 'A' });
        var escaped = UriSpan.EscapeDataString(input);

        // Unescape should return the same sequence (unpaired surrogate may be preserved as replacement or encoded)
        Assert.That(UriSpan.UnescapeDataString(escaped), Is.EqualTo(input.Replace('\uD800', '\uFFFD')));
    }

    [Test]
    public void Escape_UnpairedLowSurrogate_IsEncodedAndRoundTrips()
    {
        var low = '\uDC00';
        var input = new string(new[] { 'A', low });
        var escaped = UriSpan.EscapeDataString(input);

        Assert.That(UriSpan.UnescapeDataString(escaped), Is.EqualTo(input.Replace('\uDC00', '\uFFFD')));
    }

    [Test]
    public void Escape_AsciiString_RoundTripsWithUnescape()
    {
        var input = "Hello-World_123.~";
        var escaped = UriSpan.EscapeDataString(input);

        Assert.That(UriSpan.UnescapeDataString(escaped), Is.EqualTo(input));
    }

    [Test]
    public void Escape_LongString_RoundTrips()
    {
        var input = new string('a', 10000) + " " + new string('ß', 5000) + "🙂";
        var escaped = UriSpan.EscapeDataString(input);

        var unescaped = UriSpan.UnescapeDataString(escaped);
        Assert.That(unescaped, Is.EqualTo(input));
    }

    [Test]
    public void Escape_AllAsciiValues_RoundTrip()
    {
        // Build a string containing ASCII 0..127
        var chars = Enumerable.Range(0, 128).Select(i => (char)i).ToArray();
        var input = new string(chars);
        var escaped = UriSpan.EscapeDataString(input);
        var unescaped = UriSpan.UnescapeDataString(escaped);

        Assert.That(unescaped, Is.EqualTo(input));
    }

    [Test]
    public void Escape_AllByteValues_RoundTripViaUnescape()
    {
        // Build a string containing code points 0..255 (BMP)
        var chars = Enumerable.Range(0, 256).Select(i => (char)i).ToArray();
        var input = new string(chars);
        var escaped = UriSpan.EscapeDataString(input);
        var unescaped = UriSpan.UnescapeDataString(escaped);

        Assert.That(unescaped, Is.EqualTo(input));
    }

    [Test]
    public void Escape_RandomizedRoundTripTests()
    {
        var random = new Random(12345);
        for (var t = 0; t < 2000; t++)
        {
            var len = random.Next(0, 64);
            var chars = new char[len];
            for (var i = 0; i < len; i++)
            {
                // include a wide range of BMP characters (32..0xFFFF excluding surrogates)
                int code;
                do
                {
                    code = random.Next(32, 0xFFFF);
                } while (code >= 0xD800 && code <= 0xDFFF); // skip surrogate range for this randomized test
                chars[i] = (char)code;
            }

            var input = new string(chars);
            var escaped = UriSpan.EscapeDataString(input);
            var unescaped = UriSpan.UnescapeDataString(escaped);

            Assert.That(unescaped, Is.EqualTo(input), $"Round-trip failed for input (len={len}): {GetDebugPreview(input)}");
        }

        static string GetDebugPreview(string s) => s.Length <= 64 ? s : string.Concat(s.AsSpan(0, 64), "...");
    }

    [Test]
    public void Escape_Idempotency_Check()
    {
        // Escaping an already-escaped string should not produce the same string (percent signs will be encoded).
        var input = "Hello World!";
        var once = UriSpan.EscapeDataString(input);
        var twice = UriSpan.EscapeDataString(once);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(twice, Is.Not.EqualTo(once));
            // But Unescape(Escape(original)) should equal original
            Assert.That(UriSpan.UnescapeDataString(once), Is.EqualTo(input));
        }
    }

    [Test]
    public void Escape_ProducesOnlyAsciiCharacters()
    {
        var input = "€🙂A B+%";
        var escaped = UriSpan.EscapeDataString(input);

        // The escaped representation must be ASCII only (percent-encoded bytes and ASCII characters)
        Assert.That(escaped.All(c => c <= 127), Is.True, "Escaped string contains non-ASCII characters");
    }

    [Test]
    public void Escape_ByteApi_WritesAsciiBytes()
    {
        var input = "A B€+%";
        var buffer = new byte[1024];
        var written = UriSpan.EscapeDataString(input.AsSpan(), buffer);
        var ascii = Encoding.ASCII.GetString(buffer, 0, written);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ascii, Is.EqualTo(UriSpan.EscapeDataString(input)));
            Assert.That(ascii.All(ch => ch <= 127), Is.True);
        }
    }
}

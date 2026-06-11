using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace UrlEncodedToJson.Tests;

using NUnit.Framework;
using System;
using System.Text;

[TestFixture]
[SuppressMessage("Naming", "CA1707:Bezeichner dürfen keine Unterstriche enthalten")]
public class UriSpanTests
{
    [Test]
    public void Unescape_NoEscapes_ReturnsSameString()
    {
        var input = "HelloWorld123";
        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(input));
    }

    [Test]
    public void Unescape_SimplePercentEncoding()
    {
        var input = "Hello%20World";
        var expected = "Hello World";

        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_PlusBecomesSpace()
    {
        var input = "Hello+World";
        var expected = "Hello World";

        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_MixedPlusAndPercent()
    {
        var input = "Hello+World%21";
        var expected = "Hello World!";

        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_MultiplePercentSequences()
    {
        var input = "%41%42%43";
        var expected = "ABC";

        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_Utf8MultiByteCharacter()
    {
        var input = "%E2%82%AC"; // €
        var expected = "€";

        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_Utf8MixedWithAscii()
    {
        var input = "%E2%82%AC+100";
        var expected = "€ 100";

        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_IncompletePercentSequence_LeftAsIs()
    {
        var input = "abc%2";
        var expected = "abc%2";

        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_InvalidHex_LeftAsIs()
    {
        var input = "abc%ZZdef";
        var expected = "abc%ZZdef";

        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_PercentAtEnd()
    {
        var input = "abc%";
        var expected = "abc%";

        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_EmptyString()
    {
        var input = "";
        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void Unescape_OnlyPlus()
    {
        var input = "++++";
        var expected = "    ";

        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_LongString_WithMixedContent()
    {
        var input = "abc%20def+ghi%21jkl%2Fmno";
        var expected = "abc def ghi!jkl/mno";

        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_AllByteValues()
    {
        var sb = new StringBuilder();

        for (var i = 0; i < 256; i++)
        {
            sb.Append('%');
            sb.Append(i.ToString("X2", CultureInfo.InvariantCulture));
        }

        var input = sb.ToString();

        var result = UriSpan.UnescapeDataString(input);

        var expected = Uri.UnescapeDataString(input.Replace("+", " "));

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Unescape_Idempotent_WhenAppliedTwice()
    {
        var input = "Hello%20World%21";
        var once = UriSpan.UnescapeDataString(input);
        var twice = UriSpan.UnescapeDataString(once);

        Assert.That(twice, Is.EqualTo(once));
    }

    [Test]
    public void Unescape_HandlesLargeInput()
    {
        var input = new string('a', 10000) + "%20" + new string('b', 10000);
        var result = UriSpan.UnescapeDataString(input);

        Assert.That(result, Has.Length.EqualTo(20001));
        Assert.That(result[10000], Is.EqualTo(' '));
    }

    [Test]
    public void Unescape_MatchesBclBehavior_RandomInputs()
    {
        var random = new Random(42);

        for (var t = 0; t < 1000; t++)
        {
            var chars = new char[50];

            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)random.Next(32, 127);
            }

            var input = new string(chars);

            var expected = Uri.UnescapeDataString(input.Replace("+", " "));
            var actual = UriSpan.UnescapeDataString(input);

            Assert.That(actual, Is.EqualTo(expected), $"Mismatch for input: {input}");
        }
    }
}

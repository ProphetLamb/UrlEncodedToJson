using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using UrlEncodedToJson.Text.Json;

namespace UrlEncodedToJson.Serialization;

/// <summary>
/// Arbitrary precision JSON number value.
/// Cheap for serialization &amp; deserialization.
/// Unusable for arithmetic.
/// </summary>
/// <param name="text">The numeric text.</param>
/// <param name="components">The position of number components in the <paramref name="text"/>.</param>
[JsonConverter(typeof(JsonConverter))]
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct JsonNumber(string? text, JsonNumber.NumberComponents components)
{
    public string? Text { get; } = text;
    public NumberComponents Components { get; } = components;

    public static JsonNumber Parse(string s)
    {
        return new(s, NumberComponents.Parse(s));
    }

    public static bool TryParse([NotNullWhen(true)] string? s, out JsonNumber result)
    {
        var success = NumberComponents.TryParse(s, out var components);
        result = new(success ? s : null, components);
        return success;
    }

    public override string? ToString()
    {
        return Text;
    }

    public readonly struct NumberComponents(
        char sign,
        Range unsignedInteger,
        Range fraction,
        Range exponent
    )
    {
        /// <summary>
        /// The sign character
        /// </summary>
        public char Sign => sign;

        /// <summary>
        ///  The integer without the sign
        /// </summary>
        public Range UnsignedInteger => unsignedInteger;

        /// <summary>
        /// The fractional after the integer
        /// </summary>
        public Range Fraction => fraction;

        /// <summary>
        /// The exponent including the sign
        /// </summary>
        public Range Exponent => exponent;

        public static NumberComponents Parse(ReadOnlySpan<char> s)
        {
            return TryParse(s, out var components)
                ? components
                : throw new FormatException("The text is not a valid number.");
        }

        public static bool TryParse(ReadOnlySpan<char> s, out NumberComponents components)
        {
            components = default;
            var i = s.Length - s.TrimStart().Length;
            var sign = SignValue(s, ref i);
            if (i >= s.Length)
            {
                return false;
            }

            var integer = IntegerRange(s, ref i);
            var fraction = FractionRange(s, ref i);

            // Require at least one digit, either before or after '.'
            if (s[integer].IsEmpty && s[fraction].IsEmpty)
            {
                return false;
            }

            if (ExponentRange(s, ref i) is not { } exponent)
            {
                return false;
            }

            // No trailing garbage allowed
            i += s.Length - s.TrimEnd().Length;
            if (i != s.Length)
            {
                return false;
            }

            components = new NumberComponents(
                sign,
                integer,
                fraction,
                exponent
            );
            return true;
        }

        private static char SignValue(ReadOnlySpan<char> s, ref int i)
        {
            if (i >= s.Length)
            {
                return '\0';
            }

            var c = s[i];
            if (c is not ('+' or '-'))
            {
                return '\0';
            }

            i++;
            return c;
        }

        private static Range? ExponentRange(ReadOnlySpan<char> s, ref int i)
        {
            if (i >= s.Length || (s[i] != 'e' && s[i] != 'E'))
            {
                return default(Range);
            }

            i++;
            if (i >= s.Length)
            {
                return null;
            }

            var exponentStart = i;

            if (s[i] is '+' or '-')
            {
                i++;
                if (i >= s.Length)
                {
                    return null;
                }
            }

            var exponentDigitsStart = i;
            while (i < s.Length && char.IsDigit(s[i]))
            {
                i++;
            }

            if (exponentDigitsStart == i)
            {
                return null;
            }

            return exponentStart..i;
        }

        private static Range IntegerRange(scoped ReadOnlySpan<char> s, ref int i)
        {
            var start = i;
            while (i < s.Length && char.IsDigit(s[i]))
            {
                i++;
            }

            return start..i;
        }

        private static Range FractionRange(scoped ReadOnlySpan<char> s, ref int i)
        {
            if (i >= s.Length || s[i] != '.')
            {
                return default;
            }

            i++;
            var start = i;

            while (i < s.Length && char.IsDigit(s[i]))
            {
                i++;
            }

            return start..i;
        }
    }

    private sealed class JsonConverter : JsonConverter<JsonNumber>
    {
        public override JsonNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String or JsonTokenType.Number => Parse(reader.GetValueText()),
                _ => throw new JsonException($"Expected number or string token, got {reader.TokenType}."),
            };
        }


        public override void Write(Utf8JsonWriter writer, JsonNumber value, JsonSerializerOptions options)
        {
            writer.WriteRawValue(value.Text ?? "0");
        }
    }
}

[DebuggerDisplay("{ToString(),nq}")]
internal readonly ref struct ValueJsonNumber(ReadOnlySpan<char> text, JsonNumber.NumberComponents components)
{
    public readonly ReadOnlySpan<char> Text = text;
    public readonly JsonNumber.NumberComponents Components = components;
    public char Sign => Components.Sign;
    public ReadOnlySpan<char> UnsignedInteger => Text[Components.UnsignedInteger];
    public ReadOnlySpan<char> Fraction => Text[Components.Fraction];
    public ReadOnlySpan<char> Exponent => Text[Components.Exponent];
    public bool IsInteger => Fraction.IsEmpty && Exponent.IsEmpty;
    public bool MaybeInt64 => UnsignedInteger.Length <= 19 && Fraction.IsEmpty;

    public bool MaybeUInt64 => UnsignedInteger.Length <= 20 && Fraction.IsEmpty && Sign != '-';

    // Decimal is accurate up to 29 digits, but the result is inexact for values with more than 28 digits
    public bool MaybeExactDecimal => UnsignedInteger.Length + Fraction.Length + SmallExponentAbs < 29;

    private int SmallExponentAbs => Exponent.IsEmpty ? 0 :
        int.TryParse(
            Exponent,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var exp
        ) ? Math.Abs(exp) : int.MaxValue;

    public static ValueJsonNumber Parse(ReadOnlySpan<char> s)
    {
        return new(s, JsonNumber.NumberComponents.Parse(s));
    }

    public static bool TryParse(ReadOnlySpan<char> s, out ValueJsonNumber result)
    {
        var success = JsonNumber.NumberComponents.TryParse(s, out var components);
        result = new(success ? s : default, components);
        return success;
    }

    public JsonNumber ToJsonNumber()
    {
        return new(Text.ToString(), Components);
    }

    public override string ToString()
    {
        return Text.ToString();
    }
}

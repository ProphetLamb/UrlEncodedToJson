using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrlEncodedToJson.Serialization;

/// <summary>
/// Arbitrary precision JSON number value.
/// Cheap for serialization &amp; deserialization.
/// Unusable for arithmetic.
/// </summary>
/// <param name="text">The numeric text.</param>
/// <param name="components">The position of number components in the <paramref name="text"/>.</param>
[JsonConverter(typeof(JsonConverter))]
public readonly struct JsonNumber(string? text, JsonNumber.NumberComponents components)
#if NET8_0_OR_GREATER
    : IParsable<JsonNumber>
#endif
{
    public string? Text { get; } = text;
    public NumberComponents Components { get; } = components;

    public bool IsInteger => Components.IsInteger(Text.AsSpan().Length);
    public int Sign => Components.Sign == '-' ? -1 : 1;

    [SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    public static JsonNumber Parse(string s, IFormatProvider? provider)
    {
        return new(s, NumberComponents.Parse(s));
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out JsonNumber result)
    {
        var success = NumberComponents.TryParse(s, out var components);
        result = new(success ? s : null, components);
        return success;
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

        public bool IsInteger(int length)
        {
            return Fraction.GetOffsetAndLength(length).Length == 0 && Exponent.GetOffsetAndLength(length).Length == 0;
        }

        public static NumberComponents Parse(ReadOnlySpan<char> s)
        {
            if (TryParseInternal(s, out var components) is { } edi)
            {
                edi.Throw();
            }

            return components;
        }

        public static bool TryParse(ReadOnlySpan<char> s, out NumberComponents components)
        {
            return TryParseInternal(s, out components) == null;
        }

        private static ExceptionDispatchInfo? TryParseInternal(ReadOnlySpan<char> s, out NumberComponents components)
        {
            components = default;
            var i = s.Length - s.TrimStart().Length;
            var sign = SignValue(s, ref i);
            if (i >= s.Length)
            {
                return ExceptionDispatchInfo.Capture(new FormatException("The sign '+' or '-' is not a valid number"));
            }

            var integer = IntegerRange(s, ref i);
            var fraction = FractionRange(s, ref i);

            // Require at least one digit, either before or after '.'
            if (s[integer].IsEmpty && s[fraction].IsEmpty)
            {
                return ExceptionDispatchInfo.Capture(
                    new FormatException("The number is missing the integer, and fraction component")
                );
            }

            if (ExponentRange(s, ref i) is not { } exponent)
            {
                return ExceptionDispatchInfo.Capture(
                    new FormatException("The exponent component of the number is invalid")
                );
            }

            // No trailing garbage allowed
            i += s.Length - s.TrimEnd().Length;
            if (i != s.Length)
            {
                return ExceptionDispatchInfo.Capture(
                    new FormatException("The text contains invalid data after the completed number")
                );
            }

            components = new NumberComponents(
                sign,
                integer,
                fraction,
                exponent
            );
            return null;
        }
    }

    private sealed class JsonConverter : JsonConverter<JsonNumber>
    {
        public override JsonNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = GetTokenText(ref reader);
            return text is null ? default : Parse(text, CultureInfo.InvariantCulture);
        }

        public override void Write(Utf8JsonWriter writer, JsonNumber value, JsonSerializerOptions options)
        {
            writer.WriteRawValue(value.Text ?? "0");
        }

        private static string? GetTokenText(ref Utf8JsonReader reader)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String or JsonTokenType.Number => reader.GetString(),
                _ => throw new JsonException($"Expected number or string token, got {reader.TokenType}.")
            };
        }
    }
}

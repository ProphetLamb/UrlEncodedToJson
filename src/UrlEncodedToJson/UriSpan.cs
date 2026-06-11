using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace UrlEncodedToJson;

internal static class UriSpan
{
    /// <inheritdoc cref="UnescapeDataString(ReadOnlySpan{char}, string)"/>
    public static string UnescapeDataString(scoped ReadOnlySpan<char> input)
    {
        return UnescapeDataString(input, null);
    }

    /// <inheritdoc cref="UnescapeDataString(ReadOnlySpan{char}, string)"/>
    public static string UnescapeDataString(string input)
    {
        return UnescapeDataString(input, input);
    }

    /// <summary>
    /// Bespoke `Uri.UnescapeDataString` implementation.
    /// <list type="bullet">
    ///  <item>decodes `+` as ` `</item>
    ///  <item>.NET doesn't support `ReadOnlySpan{char}` until .NET10</item>
    /// </list>
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="backingString">A string equal to <paramref name="input"/>.</param>
    /// <returns></returns>
    private static string UnescapeDataString(scoped ReadOnlySpan<char> input, string? backingString)
    {
        var firstNotableIndex = input.IndexOfAny("%+");
        if (firstNotableIndex < 0)
        {
            return backingString ?? new string(input);
        }

        var pooled = input.Length > 512 ? ArrayPool<char>.Shared.Rent(input.Length) : null;
        var initialBuffer = pooled ?? stackalloc char[input.Length];
        var vsb = new ValueStringBuilder(initialBuffer, pooled);
        Span<byte> rune = stackalloc byte[4];

        try
        {
            vsb.Append(input[..firstNotableIndex]);

            for (var i = firstNotableIndex; i < input.Length; i++)
            {
                switch (input[i])
                {
                    case '%':
                    {
                        var start = i;
                        var runeBytes = 0;

                        while (runeBytes < rune.Length && i + 2 < input.Length && IsHex(input[i + 1]) && IsHex(input[i + 2]))
                        {
                            rune[runeBytes++] =
                                (byte)((FromHex(input[i + 1]) << 4) | FromHex(input[i + 2]));

                            i += 3;

                            if (i >= input.Length || input[i] != '%')
                            {
                                break;
                            }
                        }

                        if (runeBytes > 0)
                        {
                            var rawLength = runeBytes * 3;
                            var chars = input.Slice(start, rawLength);
                            AppendRuneOrChars(ref vsb, rune[..runeBytes], chars);
                            i = start + rawLength - 1;
                        }
                        else
                        {
                            vsb.Append('%');
                        }

                        break;
                    }
                    case '+':
                        vsb.Append(' ');
                        break;
                    default:
                        vsb.Append(input[i]);
                        break;
                }
            }

            return vsb.ToString();
        }
        finally
        {
            vsb.Dispose();
        }
    }

    private static void AppendRuneOrChars(
        scoped ref ValueStringBuilder vsb,
        Span<byte> rune,
        ReadOnlySpan<char> chars)
    {
        for (var pivot = 0; pivot < rune.Length;)
        {
            // Validate UTF8
            if (
#if NETCOREAPP3_0_OR_GREATER
                Rune.DecodeFromUtf8(rune[pivot..], out _, out var consumed) == System.Buffers.OperationStatus.Done
#else
                TryDecodeUtf8(rune[pivot..], out var consumed)
#endif
            )
            {
                // Decode validation
                AppendUtf8(ref vsb, rune.Slice(pivot, consumed));

                pivot += consumed;
                continue;
            }

            // Emit raw %xx
            vsb.Append(chars.Slice(pivot * 3, 3));

            pivot += 1;
        }
    }

    private static void AppendUtf8(ref ValueStringBuilder vsb, ReadOnlySpan<byte> bytes)
    {
        var maxCharCount = Encoding.UTF8.GetMaxCharCount(bytes.Length);
        var dest = vsb.AppendSpan(maxCharCount);
        var written = Encoding.UTF8.GetChars(bytes, dest);
        vsb.Length -= maxCharCount - written;
    }

#if !NETCOREAPP3_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryDecodeUtf8(ReadOnlySpan<byte> source, out int bytesConsumed)
    {
        if (source.IsEmpty)
        {
            bytesConsumed = 0;
            return false;
        }

        var b0 = source[0];

        // ASCII fast path
        if (b0 <= 0x7F)
        {
            bytesConsumed = 1;
            return true;
        }

        // Must be [C2..F4]
        if (b0 is < 0xC2 or > 0xF4)
        {
            bytesConsumed = 1;
            return false;
        }

        // Need at least 1 continuation byte
        if (source.Length < 2)
        {
            bytesConsumed = 1;
            return false;
        }

        var b1 = source[1];
        if ((b1 & 0xC0) != 0x80)
        {
            bytesConsumed = 1;
            return false;
        }

        // 2-byte sequence
        if (b0 <= 0xDF)
        {
            bytesConsumed = 2;
            return true;
        }

        // Need at least 3 bytes
        if (source.Length < 3)
        {
            bytesConsumed = 1;
            return false;
        }

        var b2 = source[2];
        if ((b2 & 0xC0) != 0x80)
        {
            bytesConsumed = 1;
            return false;
        }

        // Overlong + surrogate checks (based on first two bytes)
        if (b0 == 0xE0 && b1 < 0xA0)
        {
            return Invalid(out bytesConsumed);
        }

        if (b0 == 0xED && b1 >= 0xA0)
        {
            return Invalid(out bytesConsumed);
        }

        // 3-byte sequence
        if (b0 <= 0xEF)
        {
            bytesConsumed = 3;
            return true;
        }

        // Need 4 bytes
        if (source.Length < 4)
        {
            bytesConsumed = 1;
            return false;
        }

        var b3 = source[3];
        if ((b3 & 0xC0) != 0x80)
        {
            bytesConsumed = 1;
            return false;
        }

        // Overlong + max range checks
        if (b0 == 0xF0 && b1 < 0x90)
        {
            return Invalid(out bytesConsumed);
        }

        if (b0 == 0xF4 && b1 >= 0x90)
        {
            return Invalid(out bytesConsumed);
        }

        // Valid 4-byte
        bytesConsumed = 4;
        return true;

        static bool Invalid(out int consumed)
        {
            consumed = 1;
            return false;
        }
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHex(char c)
    {
        return (uint)(c - '0') <= 9 || (uint)((c | 0x20) - 'a') <= 5;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FromHex(char c)
    {
        return c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;
    }
}

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    /// <returns>The unescaped query string.</returns>
    private static string UnescapeDataString(scoped ReadOnlySpan<char> input, string? backingString)
    {
        var firstNotableIndex = input.IndexOfAny('%', '+');
        if (firstNotableIndex < 0)
        {
            return backingString ?? new string(input);
        }

        var pooled = input.Length > 512 ? ArrayPool<char>.Shared.Rent(input.Length) : null;
        ValueStringBuilder vsb = new(pooled ?? stackalloc char[input.Length], pooled);

        vsb.Append(input[..firstNotableIndex]);

        for (var i = firstNotableIndex; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '%')
            {
                i += AppendSequence(ref vsb, input[i..]);
            }
            else
            {
                vsb.Append(c == '+' ? ' ' : c);
            }
        }

        return vsb.ToStringAndDispose();
    }

    private static int AppendSequence(scoped ref ValueStringBuilder vsb, ReadOnlySpan<char> input)
    {
        uint rune = 0;
        for (int r = 0, i = 0; r < 4; r++, i += 3)
        {
            if (i + 2 >= input.Length || input[i] != '%')
            {
                goto Invalid;
            }

            var hi = FromHex(input[i + 1]);
            var lo = FromHex(input[i + 2]);
            if (hi > 0xf || lo > 0xf)
            {
                goto Invalid;
            }

            rune |= ((hi << 4) | lo) << (r * 8);
            var validity = MeasureRune(rune, out var validRuneBytes);
            if (validity == OperationStatus.Done)
            {
                // the %xx is a valid rune -> write rune
                AppendRune(ref vsb, rune, validRuneBytes);
                return (validRuneBytes * 3) - 1;
            }

            if (validity == OperationStatus.NeedMoreData)
            {
                // need more %xx to fill a rune -> next %xx
                Debug.Assert(r < 3);
                continue;
            }

            goto Invalid;
        }

        return 0;
        Invalid:
        vsb.Append(input[0]);
        return 0;
    }

    private static void AppendRune(ref ValueStringBuilder vsb, uint rune, int byteCount)
    {
        var dest = vsb.AppendSpan(4);
        var bytes = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref rune), byteCount);
        var written = Encoding.UTF8.GetChars(bytes, dest);
        vsb.Length -= 4 - written;
    }

    private static OperationStatus MeasureRune(uint rune, out int bytesConsumed)
    {
        // Based on Rune.DecodeFromUtf8
        var b0 = (byte)rune;
        var b1 = (byte)(rune >> 0x08);
        var b2 = (byte)(rune >> 0x10);
        var b3 = (byte)(rune >> 0x18);
        bytesConsumed = 1;
        // ASCII fast path
        if (b0 <= 0x7F)
        {
            return OperationStatus.Done;
        }

        // Must be [C2..F4]
        if (b0 is < 0xC2 or > 0xF4)
        {
            return OperationStatus.InvalidData;
        }

        bytesConsumed = 2;
        // Need at least 1 continuation byte
        if (b1 == 0)
        {
            return OperationStatus.NeedMoreData;
        }

        if ((b1 & 0xC0) != 0x80)
        {
            return OperationStatus.InvalidData;
        }

        // 2-byte sequence
        if (b0 <= 0xDF)
        {
            return OperationStatus.Done;
        }

        bytesConsumed = 3;
        // Need at least 3 bytes
        if (b2 == 0)
        {
            return OperationStatus.NeedMoreData;
        }

        if ((b2 & 0xC0) != 0x80)
        {
            return OperationStatus.InvalidData;
        }

        // Overlong + surrogate checks (based on first two bytes)
        if (b0 == 0xE0 && b1 < 0xA0)
        {
            return OperationStatus.InvalidData;
        }

        if (b0 == 0xED && b1 >= 0xA0)
        {
            return OperationStatus.InvalidData;
        }

        // 3-byte sequence
        if (b0 <= 0xEF)
        {
            return OperationStatus.Done;
        }

        bytesConsumed = 4;
        // Need 4 bytes
        if (b3 == 0)
        {
            return OperationStatus.NeedMoreData;
        }

        if ((b3 & 0xC0) != 0x80)
        {
            return OperationStatus.InvalidData;
        }

        // Overlong + max range checks
        if (b0 == 0xF0 && b1 < 0x90)
        {
            return OperationStatus.InvalidData;
        }

        if (b0 == 0xF4 && b1 >= 0x90)
        {
            return OperationStatus.InvalidData;
        }

        // Valid 4-byte
        return OperationStatus.Done;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FromHex(char c)
    {
        uint v = c;
        var digit = v - '0';
        if (digit < 10)
        {
            return digit;
        }

        var lower = v | 0x20;
        var letter = lower - 'a';
        if (letter < 6)
        {
            return letter + 10;
        }

        return 0x10;
    }
}

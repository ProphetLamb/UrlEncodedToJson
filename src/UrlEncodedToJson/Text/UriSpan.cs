using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UrlEncodedToJson.Serialization;

namespace UrlEncodedToJson.Text;

internal static class UriSpan
{
    /// <inheritdoc cref="UnescapeDataStringInplace(int, ReadOnlySpan{char}, Span{char})"/>
    public static string UnescapeDataString(scoped ReadOnlySpan<char> s)
    {
        return UnescapeDataString(s, null);
    }

    /// <inheritdoc cref="UnescapeDataStringInplace(int, ReadOnlySpan{char}, Span{char})"/>
    public static string UnescapeDataString(string s)
    {
        return UnescapeDataString(s, s);
    }

    /// <inheritdoc cref="UnescapeDataStringInplace(int, ReadOnlySpan{char}, Span{char})"/>
    public static int UnescapeDataStringInplace(scoped ReadOnlySpan<char> s, scoped Span<char> output)
    {
        var firstNotableIndex = s.IndexOfAny('%', '+');
        if (firstNotableIndex >= 0)
        {
            return UnescapeDataStringInplace(firstNotableIndex, s, output);
        }

        return -1;
    }

    private static string UnescapeDataString(scoped ReadOnlySpan<char> s, string? backingString)
    {
        var firstNotableIndex = s.IndexOfAny('%', '+');
        if (firstNotableIndex < 0)
        {
            return backingString ?? new string(s);
        }

        var pooled = s.Length > JsonConstants.StackallocCharLimit
            ? ArrayPool<char>.Shared.Rent(s.Length)
            : null;
        var chars = pooled ?? stackalloc char[s.Length];
        var written = UnescapeDataStringInplace(firstNotableIndex, s, chars);
        var result = chars[..written].ToString();
        if (pooled != null)
        {
            ArrayPool<char>.Shared.Return(pooled);
        }

        return result;
    }

    /// <summary>
    /// Bespoke `Uri.UnescapeDataString` implementation.
    /// <list type="bullet">
    ///  <item>decodes `+` as ` `</item>
    ///  <item>.NET doesn't support `ReadOnlySpan{char}` until .NET10</item>
    /// </list>
    /// </summary>
    private static int UnescapeDataStringInplace(int firstNotableIndex, scoped ReadOnlySpan<char> s, scoped Span<char> output)
    {
        Debug.Assert(output.Length >= s.Length, "Output buffer must fit at least the input buffer.");
        ValueStringBuilder vsb = new(output);
        vsb.Append(s[..firstNotableIndex]);

        for (var i = firstNotableIndex; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '%')
            {
                i += AppendSequence(ref vsb, s[i..]);
            }
            else
            {
                vsb.Append(c == '+' ? ' ' : c);
            }
        }

        return vsb.Length;
    }

    private static int AppendSequence(scoped ref ValueStringBuilder vsb, scoped ReadOnlySpan<char> input)
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
        var dest = vsb.AppendSpan(byteCount);
        var bytes = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref rune), byteCount);
        var written = Encoding.UTF8.GetChars(bytes, dest);
        vsb.Length -= byteCount - written;
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

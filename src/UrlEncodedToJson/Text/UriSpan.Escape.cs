using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UrlEncodedToJson.Serialization;

namespace UrlEncodedToJson.Text;

internal static partial class UriSpan
{
    public static string EscapeDataString(scoped ReadOnlySpan<char> s)
    {
        var maxByteCount = s_encoding.GetMaxByteCount(s.Length * 3);
        var pooled = maxByteCount > JsonConstants.StackallocByteLimit ? ArrayPool<byte>.Shared.Rent(maxByteCount) : null;
        var bytes = pooled ?? stackalloc byte[maxByteCount];
        var written = EscapeDataString(s, bytes);
        var result = s_encoding.GetString(bytes[..written]);
        if (pooled != null)
        {
            ArrayPool<byte>.Shared.Return(pooled);
        }
        return result;
    }

    /// <summary>
    /// Escapes into provided output buffer. Output must be large enough (at least s_encoding.GetMaxByteCount(input.Length * 3)).
    /// Returns number of bytes written.
    /// <br/>Encodes ` ` as `+`
    /// </summary>
    public static int EscapeDataString(scoped ReadOnlySpan<char> s, scoped Span<byte> utf8Bytes)
    {
        Debug.Assert(utf8Bytes.Length >= s_encoding.GetMaxByteCount(s.Length * 3), "Output buffer must be large enough for worst-case expansion.");
        var o = 0;

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];

            if (c == ' ')
            {
                utf8Bytes[o++] = (byte)'+';
                continue;
            }

            if (IsUnreserved(c))
            {
                utf8Bytes[o++] = (byte)c;
                continue;
            }

            // For '+' and '%' and any other non-unreserved char, percent-encode UTF-8 bytes.
            // Handle surrogate pairs
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    // encode surrogate pair as UTF-8
                    var high = c;
                    var low = s[++i];
                    var chars = ((uint)low << 16) | high;
                    // encode to bytes
                    AppendEncodedText(utf8Bytes, ref o, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, char>(ref chars), 2));
                    continue;
                }

                // Unpaired high surrogate -> treat as replacement by percent-encoding its UTF-8 of the single char
                AppendEncodedText(utf8Bytes, ref o, MemoryMarshal.CreateReadOnlySpan(ref c, 1));
                continue;
            }

            if (char.IsLowSurrogate(c))
            {
                // Unpaired low surrogate -> percent-encode its UTF-8 bytes for the single char
                AppendEncodedText(utf8Bytes, ref o, MemoryMarshal.CreateReadOnlySpan(ref c, 1));
                continue;
            }

            // BMP non-surrogate char
            if (c <= 0x7F)
            {
                // ASCII non-unreserved -> single byte percent-encode
                AppendEncodedByte(utf8Bytes, ref o, (byte)c);
            }
            else
            {
                // Non-ASCII BMP -> UTF-8 multi-byte
                AppendEncodedText(utf8Bytes, ref o, MemoryMarshal.CreateReadOnlySpan(ref c, 1));
            }
        }

        return o;
    }

    private static void AppendEncodedText(Span<byte> output, ref int o, ReadOnlySpan<char> text)
    {
        Span<byte> bytes = stackalloc byte[s_encoding.GetMaxByteCount(text.Length)];
        foreach (var b in bytes[..s_encoding.GetBytes(text, bytes)])
        {
            AppendEncodedByte(output, ref o, b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendEncodedByte(Span<byte> output, ref int o, byte value)
    {
        output[o++] = (byte)'%';
        output[o++] = (byte)ToHexUpper((value >> 4) & 0xF);
        output[o++] = (byte)ToHexUpper(value & 0xF);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char ToHexUpper(int v)
    {
        return (char)(v < 10 ? '0' + v : 'A' + (v - 10));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUnreserved(char c)
    {
        // unreserved = ALPHA / DIGIT / "-" / "." / "_" / "~"
        if ((char)(c | 0x20) is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
        {
            return true;
        }

        return c is '-' or '.' or '_' or '~';
    }
}

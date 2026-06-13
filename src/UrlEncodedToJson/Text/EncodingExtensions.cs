using System.Buffers;

// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130
namespace System.Text;
#pragma warning restore IDE0130

internal static class EncodingExtensions
{
#if !NET8_0_OR_GREATER
    public static int GetChars(this Encoding encoding, in ReadOnlySequence<byte> bytes, Span<char> chars)
    {
        int written;
        if (bytes.IsSingleSegment)
        {
            // If the incoming sequence is single-segment, one-shot this.
            written = encoding.GetChars(bytes.FirstSpan, chars);
        }
        else
        {
            // If the incoming sequence is multi-segment, create a stateful Decoder
            // and use it as the workhorse. On the final iteration we'll pass flush=true.

            var remainingBytes = bytes;
            var originalCharsLength = chars.Length;
            var decoder = encoding.GetDecoder();
            bool isFinalSegment;

            do
            {
                var firstSpan = remainingBytes.FirstSpan;
                var next = remainingBytes.GetPosition(firstSpan.Length);
                isFinalSegment = remainingBytes.IsSingleSegment;

                var charsWrittenJustNow = decoder.GetChars(firstSpan, chars, flush: isFinalSegment);
                chars = chars.Slice(charsWrittenJustNow);
                remainingBytes = remainingBytes.Slice(next);
            } while (!isFinalSegment);

            written = originalCharsLength - chars.Length; // total number of chars we wrote
        }

        return written;
    }
#endif
}

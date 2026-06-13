using System.Buffers;
using System.Text;
using System.Text.Json;
using UrlEncodedToJson.Serialization;

namespace UrlEncodedToJson.Text.Json;

internal static class JsonReaderExtensions
{
    public static string GetValueText(this Utf8JsonReader reader)
    {
        var maxCharsCount = Encoding.UTF8.GetMaxCharCount(
            reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length
        );
        var pooled = maxCharsCount > JsonConstants.StackallocCharLimit
            ? ArrayPool<char>.Shared.Rent(maxCharsCount)
            : null;
        var chars = pooled ?? stackalloc char[maxCharsCount];
        var written = reader.HasValueSequence
            ? Encoding.UTF8.GetChars(reader.ValueSequence, chars)
            : Encoding.UTF8.GetChars(reader.ValueSpan, chars);
        chars = chars[..written];
        var result = chars.ToString();
        if (pooled != null)
        {
            ArrayPool<char>.Shared.Return(pooled);
        }

        return result;
    }
}

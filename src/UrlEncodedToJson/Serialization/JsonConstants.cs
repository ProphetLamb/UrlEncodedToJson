namespace UrlEncodedToJson.Serialization;

internal static class JsonConstants
{
    public const int StackallocByteLimit = 512;
    public const int StackallocCharLimit = 256;

    public static bool IsNullLiteral(ReadOnlySpan<char> value)
    {
        return value.Equals("null", StringComparison.Ordinal);
    }

    public static bool IsNamedFloatingPointLiteral(ReadOnlySpan<char> rawText)
    {
        return rawText.Equals("Infinity", StringComparison.Ordinal) ||
               rawText.Equals("-Infinity", StringComparison.Ordinal) ||
               rawText.Equals("NaN", StringComparison.Ordinal);
    }
}

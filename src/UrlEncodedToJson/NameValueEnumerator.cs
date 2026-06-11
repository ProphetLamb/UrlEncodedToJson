namespace UrlEncodedToJson;

internal readonly ref struct NameValue(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
{
    public ReadOnlySpan<char> Key { get; } = key;

    public ReadOnlySpan<char> Value { get; } = value;

    public void Deconstruct(out ReadOnlySpan<char> key, out ReadOnlySpan<char> value)
    {
        key = Key;
        value = Value;
    }
}

internal readonly ref struct NameValueEnumerable(ReadOnlySpan<char> query)
{
    private readonly ReadOnlySpan<char> _query = query;

    public NameValueEnumerator GetEnumerator()
    {
        return new NameValueEnumerator(_query);
    }
}

internal ref struct NameValueEnumerator(ReadOnlySpan<char> query)
{
    private ReadOnlySpan<char> _remaining = query;

    public NameValue Current { get; private set; } = default;

    public bool MoveNext()
    {
        while (!_remaining.IsEmpty)
        {
            var separatorIndex = IndexOfQuerySeparator(_remaining, out var separatorLength);

            ReadOnlySpan<char> pair;

            if (separatorIndex >= 0)
            {
                pair = _remaining[..separatorIndex];
                _remaining = _remaining[(separatorIndex + separatorLength)..];
            }
            else
            {
                pair = _remaining;
                _remaining = "";
            }

            if (pair.IsEmpty)
            {
                continue;
            }

            var equalsIndex = pair.IndexOf('=');

            Current = equalsIndex >= 0
                ? new(pair[..equalsIndex], pair[(equalsIndex + 1)..])
                : new(pair, "");

            return true;
        }

        return false;
    }

    private static int IndexOfQuerySeparator(
        ReadOnlySpan<char> query,
        out int separatorLength)
    {
        for (var i = 0; i < query.Length; i++)
        {
            if (query[i] != '&')
            {
                continue;
            }

            if (query[i..].StartsWith("&amp;", StringComparison.OrdinalIgnoreCase))
            {
                separatorLength = 5;
                return i;
            }

            separatorLength = 1;
            return i;
        }

        separatorLength = 0;
        return -1;
    }
}
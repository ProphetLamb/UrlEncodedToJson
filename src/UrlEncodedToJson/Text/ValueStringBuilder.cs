using System.Buffers;

namespace UrlEncodedToJson.Text;

internal ref struct ValueStringBuilder(Span<char> initialBuffer, char[]? arrayFromPool = null)
{
    private Span<char> _buffer = initialBuffer;
    private char[]? _arrayFromPool = arrayFromPool;
    public int Length { get; set; } = 0;

    public void Append(char c)
    {
        if (Length >= _buffer.Length)
        {
            Grow(1);
        }

        _buffer[Length++] = c;
    }

    public void Append(scoped ReadOnlySpan<char> span)
    {
        if (Length + span.Length > _buffer.Length)
        {
            Grow(span.Length);
        }

        span.CopyTo(_buffer[Length..]);
        Length += span.Length;
    }

    public Span<char> AppendSpan(int length)
    {
        if (Length + length > _buffer.Length)
        {
            Grow(length);
        }

        var span = _buffer.Slice(Length, length);
        Length += length;
        return span;
    }

    private void Grow(int additionalCapacity)
    {
        var newSize = Math.Max(_buffer.Length * 2, Length + additionalCapacity);
        var newArray = ArrayPool<char>.Shared.Rent(newSize);

        _buffer[..Length].CopyTo(newArray);

        if (_arrayFromPool != null)
        {
            ArrayPool<char>.Shared.Return(_arrayFromPool);
        }

        _buffer = newArray;
        _arrayFromPool = newArray;
    }

    public string ToStringAndDispose()
    {
        var s = ToString();
        Dispose();
        return s;
    }

    public override readonly string ToString()
    {
        return new string(_buffer[..Length]);
    }

    public void Dispose()
    {
        if (_arrayFromPool != null)
        {
            ArrayPool<char>.Shared.Return(_arrayFromPool);
            _arrayFromPool = null;
        }
    }
}

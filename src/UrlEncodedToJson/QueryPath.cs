using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UrlEncodedToJson.Serialization;
using UrlEncodedToJson.Text;

namespace UrlEncodedToJson;

internal enum QueryPathConnection
{
    Literal,
    Field,
    Index
}

[DebuggerDisplay("{ToString(),nq}")]
internal sealed class QueryPath(
    QueryPath? parent,
    string? key,
    int index,
    QueryPathConnection connection,
    int depth
) : IEquatable<QueryPath>
{
    private string? _toString;

    public QueryPath? Parent => parent;
    public string? Key => key;
    public int Index => index;
    public int Depth => depth;
    public QueryPathConnection Connection => connection;

    [field: AllowNull] public static QueryPath Root => field ??= Literal("$");

    public static QueryPath Literal(string path)
    {
        return new(
            null,
            path,
            -1,
            QueryPathConnection.Literal,
            0
        );
    }

    public override string ToString()
    {
        return _toString ??= CreateToString();
    }

    public QueryPath this[string childKey] => new(
        this,
        childKey,
        -1,
        QueryPathConnection.Field,
        Depth + 1
    );

    public QueryPath this[int childIndex] => new(
        this,
        null,
        childIndex,
        QueryPathConnection.Index,
        Depth + 1
    );

    private string CreateToString()
    {
        var p = Parent?.ToString() ?? "";
        var prefix = Connection switch
        {
            QueryPathConnection.Field => ".",
            QueryPathConnection.Index => "[",
            _ => ""
        };
        var infix = Connection switch
        {
            QueryPathConnection.Field => Key,
            QueryPathConnection.Index => Index.ToString(CultureInfo.InvariantCulture),
            _ => Key
        };
        var postfix = Connection switch
        {
            QueryPathConnection.Index => "]",
            _ => ""
        };
        return $"{p}{prefix}{infix}{postfix}";
    }

    public bool Equals(QueryPath? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || Depth != other.Depth)
        {
            return false;
        }

        for (var self = this; self != null && other != null; self = self.Parent, other = other.Parent)
        {
            var equals = self.Connection == other.Connection
                         && self.Index == other.Index
                         && StringComparer.Ordinal.Equals(self.Key, other.Key);
            if (!equals)
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || (obj is QueryPath other && Equals(other));
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            index,
            FieldNameHash(key),
            depth
        );
    }

    public bool Equals(ReadOnlySpan<char> other)
    {
        return !other.IsEmpty && AreEqual(other, this);
    }

    private bool AreEqual(ReadOnlySpan<char> s, QueryPath? t)
    {
        while (!s.IsEmpty && t != null)
        {
            var (remaining, encodedFieldName) = UrlEncodedElementConverter.TakeLastFromPath(s);
            if (!FieldEquals(encodedFieldName, t))
            {
                return false;
            }
            s = remaining;
            t = t.Parent;
        }

        return parent == null && s.IsEmpty;

        static bool FieldEquals(ReadOnlySpan<char> encodedFieldName, QueryPath trace)
        {
            var pooled = encodedFieldName.Length > JsonConstants.StackallocCharLimit
                ? ArrayPool<char>.Shared.Rent(encodedFieldName.Length)
                : null;
            var fieldName = pooled ?? stackalloc char[encodedFieldName.Length];
            var written = UriSpan.UnescapeDataString(encodedFieldName, fieldName);
            fieldName = fieldName[..written];
            var areEqual = trace.Connection switch
            {
                QueryPathConnection.Index => int.TryParse(fieldName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index == trace.Index,
                _ => fieldName.Equals(trace.Key, StringComparison.Ordinal)
            };
            if (pooled != null)
            {
                ArrayPool<char>.Shared.Return(pooled);
            }

            return areEqual;
        }
    }

    public static int GetHashCode(ReadOnlySpan<char> other, QueryPathConnection connection)
    {
        var (remaining, fieldName) = UrlEncodedElementConverter.TakeLastFromPath(other);
        var depth = GetDepth(remaining);
        var index = connection switch
        {
            QueryPathConnection.Index => int.TryParse(fieldName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : 0,
            _ => 0
        };
        var fieldNameHash = connection switch
        {
            QueryPathConnection.Index => 0UL,
            _ => FieldNameHash(fieldName)
        };
        return HashCode.Combine(
            index,
            fieldNameHash,
            depth
        );

        static int GetDepth(ReadOnlySpan<char> trace)
        {
            var i = 0;
            while (!trace.IsEmpty)
            {
                var (_, remaining) = UrlEncodedElementConverter.TakeFromPath(trace);
                i += 1;
                trace = remaining;
            }

            return i;
        }
    }

    private static ulong FieldNameHash(ReadOnlySpan<char> fieldName)
    {
        if (fieldName.IsEmpty)
        {
            return 0;
        }
        var bytes = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(fieldName)), fieldName.Length * 2);
        return System.IO.Hashing.XxHash3.HashToUInt64(bytes, 1337);
    }
}

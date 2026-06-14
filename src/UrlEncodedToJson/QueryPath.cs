using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace UrlEncodedToJson;

internal enum QueryPathConnection
{
    Literal,
    Field,
    Index
}

[DebuggerDisplay("{ToString(),nq}")]
internal sealed class QueryPath(QueryPath? parent, string? key, int index, QueryPathConnection connection, int depth)
{
    private string? _toString;

    public QueryPath? Parent => parent;
    public string? Key => key;
    public int Index => index;
    public int Depth => depth;
    public QueryPathConnection Connection => connection;

    [field: AllowNull]
    public static QueryPath Root => field ??= Literal("$");

    public static QueryPath Literal(string path)
    {
        return new(null, path, -1, QueryPathConnection.Literal, 0);
    }

    public override string ToString()
    {
        return _toString ??= CreateToString();
    }

    public QueryPath this[string childKey] => new(this, childKey, -1, QueryPathConnection.Field, Depth + 1);

    public QueryPath this[int childIndex] => new(this, null, childIndex, QueryPathConnection.Index, Depth + 1);

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
}

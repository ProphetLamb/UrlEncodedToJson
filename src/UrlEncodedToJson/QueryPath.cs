using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace UrlEncodedToJson;

internal enum NestingTraceConnection
{
    Literal,
    Field,
    Index
}

[DebuggerDisplay("{ToString(),nq}")]
internal sealed class QueryPath(QueryPath? parent, string? key, int index, NestingTraceConnection connection, int depth)
{
    private string? _toString;

    public QueryPath? Parent => parent;
    public string? Key => key;
    public int Index => index;
    public int Depth => depth;
    public NestingTraceConnection Connection => connection;

    [field: AllowNull]
    public static QueryPath Root => field ??= Literal("$");

    public static QueryPath Literal(string path)
    {
        return new(null, path, -1, NestingTraceConnection.Literal, 0);
    }

    public override string ToString()
    {
        return _toString ??= CreateToString();
    }

    public QueryPath this[string childKey] => new(this, childKey, -1, NestingTraceConnection.Field, Depth + 1);

    public QueryPath this[int childIndex] => new(this, null, childIndex, NestingTraceConnection.Index, Depth + 1);

    private string CreateToString()
    {
        var p = Parent?.ToString() ?? "";
        var prefix = Connection switch
        {
            NestingTraceConnection.Field => ".",
            NestingTraceConnection.Index => "[",
            _ => ""
        };
        var infix = Connection switch
        {
            NestingTraceConnection.Field => Key,
            NestingTraceConnection.Index => Index.ToString(CultureInfo.InvariantCulture),
            _ => Key
        };
        var postfix = Connection switch
        {
            NestingTraceConnection.Index => "]",
            _ => ""
        };
        return $"{p}{prefix}{infix}{postfix}";
    }
}

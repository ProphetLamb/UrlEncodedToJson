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
internal sealed class NestingTrace(NestingTrace? parent, string? key, int index, NestingTraceConnection connection)
{
    private string? _toString;

    public NestingTrace? Parent => parent;
    public string? Key => key;
    public int Index => index;
    public NestingTraceConnection Connection => connection;

    [field: AllowNull]
    public static NestingTrace Root => field ??= Literal("$");

    public static NestingTrace Literal(string path)
    {
        return new(null, path, -1, NestingTraceConnection.Literal);
    }

    public override string ToString()
    {
        return _toString ??= CreateToString();
    }

    public NestingTrace this[string childKey] => new(this, childKey, -1, NestingTraceConnection.Field);

    public NestingTrace this[int childIndex] => new(this, null, childIndex, NestingTraceConnection.Index);

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

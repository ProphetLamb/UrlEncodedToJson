using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace UrlEncodedToJson;

internal enum NestingTraceConnection
{
    Literal,
    Field,
    Index
}

[DebuggerDisplay("{ToString(),nq}")]
internal sealed class NestingTrace
{
    private string? _toString;

    public NestingTrace? Parent { get; init; }
    public string? Key { get; init; }
    public int Index { get; init; }
    public required NestingTraceConnection Connection { get; init; }

    [field: AllowNull]
    public static NestingTrace Root => field ??= Literal("$");

    public static NestingTrace Literal(string path)
    {
        return new()
        {
            Connection = NestingTraceConnection.Literal,
            Key = path,
        };
    }

    public override string ToString()
    {
        return _toString ??= CreateToString();
    }

    public NestingTrace this[string key] => new()
    {
        Connection = NestingTraceConnection.Field,
        Key = key,
        Parent = this,
    };

    public NestingTrace this[int index] => new()
    {
        Connection = NestingTraceConnection.Index,
        Index = index,
        Parent = this,
    };

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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

    public static NestingTrace Literal(string path) => new()
    {
        Connection = NestingTraceConnection.Literal,
        Key = path,
    };

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
        // The recursive call is shortcircuted by dynamic programming, hence more efficient than stack iteration.
        var p = Parent?.ToString();
        StringBuilder b = new(p ?? "");
        b.Append(Connection switch
        {
            NestingTraceConnection.Field => ".",
            NestingTraceConnection.Index => "[",
            _ => ""
        });
        b.Append(Connection switch
        {
            NestingTraceConnection.Field => Key,
            NestingTraceConnection.Index => Index,
            _ => Key
        });
        b.Append(Connection switch
        {
            NestingTraceConnection.Index => "]",
            _ => ""
        });
        return b.ToString();
    }
}

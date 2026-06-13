using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace UrlEncodedToJson;

internal static class ThrowHelper
{

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidLeafTypeException(QueryPath trace, string value, JsonTypeInfo typeInfo)
    {
        throw new JsonException(
            "Unable to convert the value to the desired type: Expected a enumerable, or simple value according to metadata, but got a dictionary or object type",
            trace.ToString(),
            null,
            null
        ) { Data = { ["Value"] = value, ["TypeInfo"] = typeInfo } };
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static Type ThrowMissingElementTypeException(QueryPath trace, JsonTypeInfo typeInfo)
    {
        throw new JsonException(
            "Unable to convert the value to the desired type: Expected an enumerable or dictionary according to metadata, but got a object, simple value",
            trace.ToString(),
            null,
            null
        ) { Data = { ["TypeInfo"] = typeInfo } };
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowMaxDepthExceededException(QueryPath trace)
    {
        throw new JsonException(
            "Maximum depth exceeded",
            trace.ToString(),
            null,
            null
        );
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArrayMaxLengthExceeded()
    {
        throw new InvalidOperationException("Unable to allocate an array of the requested size");
    }
}

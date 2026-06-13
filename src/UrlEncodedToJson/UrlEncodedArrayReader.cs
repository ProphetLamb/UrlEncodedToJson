using System.Buffers;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using UrlEncodedToJson.Serialization;
using UrlEncodedToJson.Text;

namespace UrlEncodedToJson;

internal readonly ref struct UrlEncodedArrayReader(
    UrlEncodedElementConverter converter,
    JsonArray array,
    JsonTypeInfo typeInfo,
    QueryPath trace
)
{
    public void AddArrayValueEscaped(ReadOnlySpan<char> path, ReadOnlySpan<char> escapedValue)
    {
        var pooled = escapedValue.Length > JsonConstants.StackallocCharLimit
            ? ArrayPool<char>.Shared.Rent(escapedValue.Length)
            : null;
        var chars = pooled ?? stackalloc char[escapedValue.Length];
        var written = UriSpan.UnescapeDataStringInplace(escapedValue, chars);
        var value = written >= 0 ? chars[..written] : escapedValue;
        AddArrayValue(path, value);
        if (pooled != null)
        {
            ArrayPool<char>.Shared.Return(pooled);
        }
    }
    public void AddArrayValue(ReadOnlySpan<char> path, ReadOnlySpan<char> value)
    {
        converter.ThrowIfMaxDepthExceeded(trace);
        var (escapedIndex, childPath) = UrlEncodedElementConverter.TakeFromPath(path);

        if (!int.TryParse(
                escapedIndex,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var index
            )
            || index < 0)
        {
            array.Add(null);
            AddLeafValue(array.Count - 1, value);
            return;
        }

        if (index >= 0X7FFFFFC7)
        {
            ThrowHelper.ThrowArrayMaxLengthExceeded();
        }

        while (array.Count <= index)
        {
            array.Add(null);
        }

        if (childPath.IsEmpty)
        {
            AddLeafValue(index, value);
            return;
        }

        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Object:
                GetObjectReader(index).AddObjectValue(childPath, value);
                break;

            case JsonTypeInfoKind.Dictionary:
                CreateDictionaryReader(index).AddDictionaryValue(childPath, value);
                break;

            case JsonTypeInfoKind.Enumerable:
                CreateArrayReader(index).AddArrayValue(childPath, value);
                break;
            case JsonTypeInfoKind.None:
            default:
                AddLeafValue(index, value);
                break;
        }
    }

    private UrlEncodedArrayReader CreateArrayReader(int index)
    {
        var t = trace[index];
        return new(
            converter,
            GetOrCreateArray(index),
            converter.GetElementTypeInfo(typeInfo, t),
            t
        );
    }

    private UrlEncodedObjectReader CreateDictionaryReader(int index)
    {
        var t = trace[index];
        return new(
            converter,
            GetOrCreateObject(index),
            converter.GetElementTypeInfo(typeInfo, t),
            t
        );
    }

    private UrlEncodedObjectReader GetObjectReader(int index)
    {
        return new(
            converter,
            GetOrCreateObject(index),
            typeInfo,
            trace[index]
        );
    }

    private void AddLeafValue(int index, ReadOnlySpan<char> value)
    {
        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Enumerable:
                GetOrCreateArray(index).Add(converter.StringToValue(value, typeInfo));
                return;
            case JsonTypeInfoKind.None:
                array[index] = converter.StringToValue(value, typeInfo);
                return;
            case JsonTypeInfoKind.Object:
            case JsonTypeInfoKind.Dictionary:
            default:
                ThrowHelper.ThrowInvalidLeafTypeException(trace[index], value.ToString(), typeInfo);
                return;
        }
    }

    private JsonObject GetOrCreateObject(int index)
    {
        if (array[index] is JsonObject child)
        {
            return child;
        }

        child = new(converter.NodeOptions);
        array[index] = child;

        return child;
    }

    private JsonArray GetOrCreateArray(int index)
    {
        if (array[index] is JsonArray child)
        {
            return child;
        }

        child = new(converter.NodeOptions);
        array[index] = child;

        return child;
    }

    public JsonArray ToJsonArray()
    {
        return array;
    }
}

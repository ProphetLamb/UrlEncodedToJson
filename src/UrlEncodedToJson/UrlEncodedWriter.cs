using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace UrlEncodedToJson;


internal ref struct UrlEncodedWriter(UrlEncodedElementConverter converter, IBufferWriter<byte>? byteWriter, IBufferWriter<char>? charWriter)
{
    private bool _hasWrittenPair;

    public void Write(JsonElement element, JsonTypeInfo typeInfo)
    {
        WriteQueryValue(element, typeInfo, "", false);
    }

    private void WriteQueryValue(
        JsonElement element,
        JsonTypeInfo typeInfo,
        string path,
        bool forcePrimitiveArrayIndexes)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return;
        }

        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Object:
                WriteObjectQueryValue(element, typeInfo, path);
                return;
            case JsonTypeInfoKind.Dictionary:
                WriteDictionaryQueryValue(element, typeInfo, path);
                return;
            case JsonTypeInfoKind.Enumerable:
                WriteEnumerableQueryValue(element, typeInfo, path, forcePrimitiveArrayIndexes);
                return;
            case JsonTypeInfoKind.None:
            default:
                WriteScalarQueryValue(element, path);
                return;
        }
    }

    private void WriteObjectQueryValue(
        JsonElement element,
        JsonTypeInfo typeInfo,
        string path
    )
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            WriteScalarQueryValue(element, path);
            return;
        }

        foreach (var jsonProperty in element.EnumerateObject())
        {
            var propertyInfo = converter.FindProperty(typeInfo, jsonProperty.Name);

            if (propertyInfo is null)
            {
                // Same behavior as System.Text.Json deserialization:
                // ignore unknown members.
                continue;
            }

            var propertyTypeInfo = converter.GetTypeInfo(propertyInfo.PropertyType);

            WriteQueryValue(
                jsonProperty.Value,
                propertyTypeInfo,
                AppendPath(path, propertyInfo.Name),
                forcePrimitiveArrayIndexes: false
            );
        }
    }

    private void WriteDictionaryQueryValue(
        JsonElement element,
        JsonTypeInfo typeInfo,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            WriteScalarQueryValue(element, path);
            return;
        }

        var valueTypeInfo = GetElementTypeInfo(typeInfo, path);

        foreach (var jsonProperty in element.EnumerateObject())
        {
            WriteQueryValue(
                jsonProperty.Value,
                valueTypeInfo,
                AppendPath(path, jsonProperty.Name),
                forcePrimitiveArrayIndexes: false
            );
        }
    }

    private void WriteEnumerableQueryValue(
        JsonElement element,
        JsonTypeInfo typeInfo,
        string path,
        bool forcePrimitiveArrayIndexes)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            WriteScalarQueryValue(element, path);
            return;
        }

        var elementTypeInfo = GetElementTypeInfo(typeInfo, path);
        var elementIsScalar = elementTypeInfo.Kind == JsonTypeInfoKind.None;

        var index = 0;

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                index++;
                forcePrimitiveArrayIndexes = true; // when omitting null elements indexes must be used
                continue;
            }

            string itemPath;

            if (elementIsScalar)
            {
                // For normal primitive collections:
                // tags=a&tags=b
                //
                // For nested primitive collections:
                // matrix.0.1=value
                itemPath = forcePrimitiveArrayIndexes
                    ? AppendPath(path, index.ToString(CultureInfo.InvariantCulture))
                    : path;
            }
            else
            {
                // For complex collections:
                // items.0.id=12
                itemPath = AppendPath(path, index.ToString(CultureInfo.InvariantCulture));
            }

            WriteQueryValue(
                item,
                elementTypeInfo,
                itemPath,
                forcePrimitiveArrayIndexes: !elementIsScalar
            );

            index++;
        }
    }

    private void WriteScalarQueryValue(
        JsonElement element,
        string path)
    {
        var value = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",

            // Objects/arrays reaching this point usually means the declared target
            // type is object or another JsonTypeInfoKind.None type.
            // Preserve them as compact JSON.
            JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),

            JsonValueKind.Null => "null",
            _ => null
        };

        if (value is null)
        {
            return;
        }

        if (byteWriter is null)
        {
            WritePair(charWriter!, path, value);
        }
        else
        {
            WritePair(byteWriter, path, value);
        }
    }

    private void WritePair(IBufferWriter<byte> writer, string key, string value)
    {
        if (_hasWrittenPair)
        {
            writer.Write([(byte)'&']);
        }

        _hasWrittenPair = true;
        if (!string.IsNullOrEmpty(key))
        {
            var bytes = writer.GetSpan(Encoding.UTF8.GetMaxByteCount(key.Length));
            writer.Advance(Encoding.UTF8.GetBytes(key, bytes));
            writer.Write([(byte)'=']);
        }

        {
            var escapedValue = Uri.EscapeDataString(value);
            var bytes = writer.GetSpan(Encoding.UTF8.GetMaxByteCount(escapedValue.Length));
            writer.Advance(Encoding.UTF8.GetBytes(escapedValue, bytes));
        }
    }

    private void WritePair(IBufferWriter<char> writer, string key, string value)
    {
        if (_hasWrittenPair)
        {
            writer.Write("&");
        }

        _hasWrittenPair = true;
        if (!string.IsNullOrEmpty(key))
        {
            writer.Write(key);
            writer.Write("=");
        }

        writer.Write(Uri.EscapeDataString(value));
    }

    private static string AppendPath(string path, string segment)
    {
        var escapedSegment = EscapePathSegment(segment);

        return string.IsNullOrEmpty(path)
            ? escapedSegment
            : $"{path}.{escapedSegment}";
    }

    private static string EscapePathSegment(string segment)
    {
        // Uri.EscapeDataString intentionally does not escape '.',
        // but '.' is the path separator, so dictionary keys or property names
        // containing dots must encode them explicitly.
        return Uri.EscapeDataString(segment).Replace(".", "%2E", StringComparison.Ordinal);
    }

    private readonly JsonTypeInfo GetElementTypeInfo(JsonTypeInfo typeInfo, string path)
    {
        return converter.GetElementTypeInfo(typeInfo, QueryPath.Literal(path));
    }
}

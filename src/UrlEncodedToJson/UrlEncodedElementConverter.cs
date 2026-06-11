using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace UrlEncodedToJson;

[StructLayout(LayoutKind.Auto)]
internal readonly partial struct UrlEncodedElementConverter(JsonSerializerOptions options)
{
    private static readonly ConditionalWeakTable<JsonSerializerOptions, TypeCache> s_typeCacheByOptions = [];

    private readonly TypeCache _typeCache = GetOrCreateTypeCache(options);


    internal JsonNodeOptions NodeOptions => GetNodeOptions(options);

    internal JsonDocumentOptions DocumentOptions => GetDocumentOptions(options);

    [Pure]
    public string Deserialize(JsonElement element, JsonTypeInfo typeInfo)
    {
        ArrayBufferWriter<char> writer = new();
        UrlEncodedWriter queryWriter = new(this, null, writer);
        queryWriter.Write(element, typeInfo);
        return new(writer.WrittenSpan);
    }

    public void Deserialize(JsonElement element, JsonTypeInfo typeInfo, IBufferWriter<byte> writer)
    {
        UrlEncodedWriter queryWriter = new(this, writer, null);
        queryWriter.Write(element, typeInfo);
    }

    [Pure]
    public JsonNode? SerializeToNode(ReadOnlySpan<char> query, JsonTypeInfo typeInfo)
    {
        query = ExtractQueryPart(query);

        if (query.IsEmpty)
        {
            return CreateEmptyJsonValue(typeInfo);
        }


        return typeInfo.Kind switch
        {
            JsonTypeInfoKind.None => RewriteScalar(query, typeInfo),
            JsonTypeInfoKind.Object => RewriteObject(query, typeInfo),
            JsonTypeInfoKind.Enumerable => RewriteEnumerable(query, typeInfo),
            JsonTypeInfoKind.Dictionary => RewriteDictionary(query, typeInfo),
            _ => null,
        };
    }

    private static JsonNode? CreateEmptyJsonValue(JsonTypeInfo typeInfo)
    {
        return typeInfo.Kind switch
        {
            JsonTypeInfoKind.Enumerable => new JsonArray(GetNodeOptions(typeInfo.Options)),
            JsonTypeInfoKind.Object or JsonTypeInfoKind.Dictionary => new JsonObject(GetNodeOptions(typeInfo.Options)),
            _ => null,
        };
    }

    private JsonObject RewriteObject(
        ReadOnlySpan<char> query,
        JsonTypeInfo typeInfo)
    {
        UrlEncodedObjectReader root = new(this, new(NodeOptions), typeInfo, NestingTrace.Root);

        foreach (var (key, value) in new NameValueEnumerable(query))
        {
            if (key.IsEmpty)
            {
                continue;
            }

            root.AddObjectValue(
                key,
                UnescapeQueryComponent(value)
            );
        }

        return root.ToJsonObject();
    }

    private JsonArray RewriteEnumerable(
        ReadOnlySpan<char> query,
        JsonTypeInfo typeInfo)
    {
        var valueTypeInfo = GetElementTypeInfo(typeInfo, NestingTrace.Root);
        UrlEncodedArrayReader array = new(this, new(NodeOptions), valueTypeInfo, NestingTrace.Root);

        foreach (var (key, value) in new NameValueEnumerable(query))
        {
            array.AddArrayValue(
                value.IsEmpty ? "" : key,
                UnescapeQueryComponent(value.IsEmpty ? key : value)
            );
        }

        return array.ToJsonArray();
    }

    private JsonObject RewriteDictionary(
        ReadOnlySpan<char> query,
        JsonTypeInfo typeInfo)
    {
        var valueTypeInfo = GetElementTypeInfo(typeInfo, NestingTrace.Root);
        UrlEncodedObjectReader root = new(this, new(NodeOptions), valueTypeInfo, NestingTrace.Root);

        foreach (var (key, value) in new NameValueEnumerable(query))
        {
            if (key.IsEmpty)
            {
                continue;
            }

            root.AddDictionaryValue(
                key,
                UnescapeQueryComponent(value)
            );
        }

        return root.ToJsonObject();
    }

    private JsonNode? RewriteScalar(
        ReadOnlySpan<char> query,
        JsonTypeInfo typeInfo)
    {
        var enumerator = new NameValueEnumerator(query);

        if (!enumerator.MoveNext())
        {
            return null;
        }

        var (key, value) = enumerator.Current;
        var valueText = UnescapeQueryComponent(value.IsEmpty ? key : value);
        var node = StringToValue(valueText, typeInfo);
        return node;
    }

    internal static NameValue TakeFromPath(ReadOnlySpan<char> path)
    {
        var index = path.IndexOf('.');

        if (index >= 0)
        {
            return new(path[..index], path[(index + 1)..]);
        }

        return new(path, "");
    }

    internal JsonTypeInfo GetElementTypeInfo(JsonTypeInfo typeInfo, NestingTrace trace)
    {
        var elementType = typeInfo.ElementType ?? ThrowMissingElementTypeException(trace, typeInfo);
        return GetTypeInfo(elementType);
    }

    private static ReadOnlySpan<char> ExtractQueryPart(ReadOnlySpan<char> input)
    {
        var queryIndex = input.IndexOf('?');

        var queryAndFragment = queryIndex < 0
            ? input
            : input[Math.Min(input.Length, queryIndex + 1)..];

        var fragmentIndex = queryAndFragment.IndexOf('#');

        return fragmentIndex < 0
            ? queryAndFragment
            : queryAndFragment[..fragmentIndex];
    }

    internal JsonNode? StringToValue(string value, JsonTypeInfo typeInfo)
    {
        var type = Nullable.GetUnderlyingType(typeInfo.Type) ?? typeInfo.Type;
        if (type == typeof(string))
        {
            return JsonValue.Create(value, NodeOptions);
        }

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        // Handle types that serialize not to string, but to other JSON literals
        // When encountering an implausible case default to string and let json handle it

        if (type == typeof(bool))
        {
            return bool.TryParse(value, out var boolValue)
                ? JsonValue.Create(boolValue, NodeOptions)
                : JsonValue.Create(value, NodeOptions);
        }

        if (type.IsPrimitive || type == typeof(decimal) || type.IsEnum)
        {
            // Some enums are passed as string
            // Floating point literals NaN Inf -Inf are passed as string
            return (value.Length < 3 || char.IsDigit(value[1])) &&
                   double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                ? JsonNode.Parse(value, NodeOptions, DocumentOptions)
                : JsonValue.Create(value, NodeOptions);
        }

        // For the remainder it is trail and error
        // If the value is one well-formed json literal: null, a number, or a boolean; attempt to deserialize the string then serialize to node,
        // otherwise as well as on failure pass the type as a string

        // Do not handle TimeSpan or DateTime specifically as string, because they can reasonably be serialized as ticks or UNIX epoch.

        if (!value.Equals("null", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("true", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("false", StringComparison.OrdinalIgnoreCase) &&
            !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return JsonValue.Create(value, NodeOptions);
        }

        var serializeAsKind = _typeCache.CanSerializeAsKind(typeInfo);
        if ((serializeAsKind & SerializeAsKind.Boolean) != default)
        {
            if (bool.TryParse(value, out var boolValue))
            {
                return JsonValue.Create(boolValue);
            }
        }

        if ((serializeAsKind & SerializeAsKind.Number) != default)
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                return JsonValue.Create(longValue);
            }
            if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ulongValue))
            {
                return JsonValue.Create(ulongValue);
            }
            if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
            {
                return JsonValue.Create(decimalValue);
            }
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return JsonValue.Create(doubleValue);
            }
        }

        if ((serializeAsKind & SerializeAsKind.Null) != default && value.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if ((serializeAsKind & SerializeAsKind.String) != default)
        {
            return JsonValue.Create(value);
        }

        try
        {
            var reserialized = ReserializeUnsafe(value, typeInfo);
            var nodeKind = TypeCache.KindFromNode(reserialized);
            if ((serializeAsKind | nodeKind) != serializeAsKind)
            {
                _typeCache.AddSerializeAsKind(typeInfo, nodeKind);
            }
            return reserialized;
        }
        catch
        {
            return JsonValue.Create(value, NodeOptions);
        }

        static JsonNode? ReserializeUnsafe(string value, JsonTypeInfo typeInfo)
        {
            var maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
            if (maxByteCount > 512)
            {
                // no valid non string JSON token is this long
                return JsonValue.Create(value, GetNodeOptions(typeInfo.Options));
            }

            Span<byte> bytes = stackalloc byte[maxByteCount];
            bytes = bytes[..Encoding.UTF8.GetBytes(value, bytes)];
            Utf8JsonReader reader = new(bytes);
            var boxed = JsonSerializer.Deserialize(ref reader, typeInfo);
            return JsonSerializer.SerializeToNode(boxed, typeInfo);
        }
    }

    public JsonTypeInfo GetTypeInfo(Type type)
    {
        return options.GetTypeInfo(type);
    }

    internal JsonPropertyInfo? FindProperty(JsonTypeInfo typeInfo, string propertyName)
    {
        return _typeCache.FindProperty(typeInfo, propertyName);
    }

    internal static string UnescapeQueryComponent(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return "";
        }

        // Uri.UnescapeDataString does not treat '+' as space.
        // Avoid allocation unless '+' exists.
        if (value.IndexOf('+') < 0)
        {
            return Uri.UnescapeDataString(
#if NET9_0_OR_GREATER
                value
#else
                value.ToString()
#endif
            );
        }

        var pooled = value.Length > 512 ? ArrayPool<char>.Shared.Rent(value.Length) : null;
        var replaced = pooled ?? stackalloc char[value.Length];
        replaced = replaced[..value.Length];
#if NET10_0_OR_GREATER
        value.Replace(replaced, '+', ' ');
#else
        for (var i = 0; i < value.Length; i++)
        {
            replaced[i] = value[i] switch
            {
                '+' => ' ',
                var v => v,
            };
        }
#endif
        var result = Uri.UnescapeDataString(
#if NET9_0_OR_GREATER
            replaced
#else
            replaced.ToString()
#endif
        );
        if (pooled != null)
        {
            ArrayPool<char>.Shared.Return(pooled);
        }
        return result;
    }

    private static TypeCache GetOrCreateTypeCache(JsonSerializerOptions options)
    {
        return s_typeCacheByOptions.GetValue(options, static _ => new([], []));
    }

    private static JsonNodeOptions GetNodeOptions(JsonSerializerOptions options)
    {
        return new()
        {
            PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive,
        };
    }

    private static JsonDocumentOptions GetDocumentOptions(JsonSerializerOptions options)
    {
        return new()
        {
            AllowTrailingCommas = options.AllowTrailingCommas,
            AllowDuplicateProperties = options.AllowDuplicateProperties,
            CommentHandling = options.ReadCommentHandling,
            MaxDepth = options.MaxDepth,
        };
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidLeafTypeException(NestingTrace trace, string value, JsonTypeInfo typeInfo)
    {
        throw new JsonException("Unable to convert the value to the desired type: Expected a enumerable, or simple value according to metadata, but got a dictionary or object type", trace.ToString(), null, null)
        {
            Data =
            {
                ["Value"] = value,
                ["TypeInfo"] = typeInfo
            }
        };
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static Type ThrowMissingElementTypeException(NestingTrace trace, JsonTypeInfo typeInfo)
    {
        throw new JsonException("Unable to convert the value to the desired type: Expected an enumerable or dictionary according to metadata, but got a object, simple value", trace.ToString(), null, null)
        {
            Data =
            {
                ["TypeInfo"] = typeInfo
            }
        };
    }
}

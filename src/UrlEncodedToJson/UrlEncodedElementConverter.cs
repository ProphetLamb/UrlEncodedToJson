using System.Buffers;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using UrlEncodedToJson.Buffers;
using UrlEncodedToJson.Serialization;
using UrlEncodedToJson.Text;

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
        using PooledBufferWriter<char> writer = new();
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

    private JsonObject RewriteObject(ReadOnlySpan<char> query, JsonTypeInfo typeInfo)
    {
        UrlEncodedObjectReader root = new(
            this,
            new(NodeOptions),
            typeInfo,
            QueryPath.Root
        );

        foreach (var (key, value) in new NameValueEnumerable(query))
        {
            if (key.IsEmpty)
            {
                continue;
            }

            root.AddObjectValueEscaped(key, value);
        }

        return root.ToJsonObject();
    }

    private JsonArray RewriteEnumerable(ReadOnlySpan<char> query, JsonTypeInfo typeInfo)
    {
        var valueTypeInfo = GetElementTypeInfo(typeInfo, QueryPath.Root);
        UrlEncodedArrayReader array = new(
            this,
            new(NodeOptions),
            valueTypeInfo,
            QueryPath.Root
        );

        foreach (var (key, value) in new NameValueEnumerable(query))
        {
            array.AddArrayValueEscaped(value.IsEmpty ? "" : key, value.IsEmpty ? key : value);
        }

        return array.ToJsonArray();
    }

    private JsonObject RewriteDictionary(ReadOnlySpan<char> query, JsonTypeInfo typeInfo)
    {
        var valueTypeInfo = GetElementTypeInfo(typeInfo, QueryPath.Root);
        UrlEncodedObjectReader root = new(
            this,
            new(NodeOptions),
            valueTypeInfo,
            QueryPath.Root
        );

        foreach (var (key, value) in new NameValueEnumerable(query))
        {
            if (key.IsEmpty)
            {
                continue;
            }

            root.AddDictionaryValueEscaped(key, value);
        }

        return root.ToJsonObject();
    }

    private JsonNode? RewriteScalar(ReadOnlySpan<char> query, JsonTypeInfo typeInfo)
    {
        var enumerator = new NameValueEnumerator(query);

        if (!enumerator.MoveNext())
        {
            return null;
        }

        var (key, value) = enumerator.Current;
        var node = StringToValueEscaped(value.IsEmpty ? key : value, typeInfo);
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

    internal JsonTypeInfo GetElementTypeInfo(JsonTypeInfo typeInfo, QueryPath trace)
    {
        var elementType = typeInfo.ElementType ?? ThrowHelper.ThrowMissingElementTypeException(trace, typeInfo);
        return GetTypeInfo(elementType);
    }

    private static ReadOnlySpan<char> ExtractQueryPart(ReadOnlySpan<char> input)
    {
        var queryIndex = input.IndexOf('?');

        var queryAndFragment = queryIndex < 0 ? input : input[Math.Min(input.Length, queryIndex + 1)..];

        var fragmentIndex = queryAndFragment.IndexOf('#');

        return fragmentIndex < 0 ? queryAndFragment : queryAndFragment[..fragmentIndex];
    }

    internal JsonNode? StringToValueEscaped(ReadOnlySpan<char> escapedValue, JsonTypeInfo typeInfo)
    {
        var pooled = escapedValue.Length > JsonConstants.StackallocCharLimit
            ? ArrayPool<char>.Shared.Rent(escapedValue.Length)
            : null;
        var chars = pooled ?? stackalloc char[escapedValue.Length];
        var written = UriSpan.UnescapeDataString(escapedValue, chars);
        var value = written >= 0 ? chars[..written] : escapedValue;
        var result = StringToValue(value, typeInfo);
        if (pooled != null)
        {
            ArrayPool<char>.Shared.Return(pooled);
        }

        return result;
    }

    internal JsonNode? StringToValue(ReadOnlySpan<char> value, JsonTypeInfo typeInfo)
    {
        if (value.IsEmpty)
        {
            return null;
        }

        var type = Nullable.GetUnderlyingType(typeInfo.Type) ?? typeInfo.Type;
        // Handle types that do not serialize to string
        // When encountering an implausible case default to string and let json handle it
        var maybeNull = type != typeInfo.Type || type.IsClass;
        if (type == typeof(string) || type == typeof(char))
        {
            return CreateStringNode(value, maybeNull);
        }

        if (type == typeof(bool))
        {
            return CreateBooleanNode(value) ?? CreateStringNode(value, maybeNull);
        }

        if (type.IsPrimitive || type == typeof(decimal) || type.IsEnum)
        {
            return CreateNumberNode(value) ?? CreateStringNode(value, maybeNull);
        }

        // Unable to statically analyze the JSON value for the type
        // We have to learn by deserializing the value, then serializing to node inspecting the JSON value kind.
        // The result is then remembered to short circuit later calls.
        var serializeAsKind = _typeCache.CanSerializeAsKind(typeInfo);

        if ((serializeAsKind & SerializeAsKind.Null) != default && JsonConstants.IsNullLiteral(value))
        {
            return null;
        }

        if ((serializeAsKind & SerializeAsKind.Boolean) != default && CreateBooleanNode(value) is { } booleanNode)
        {
            return booleanNode;
        }

        if ((serializeAsKind & SerializeAsKind.Number) != default && CreateNumberNode(value) is { } numberNode)
        {
            return numberNode;
        }

        if ((serializeAsKind & SerializeAsKind.String) != default)
        {
            return CreateStringNode(value, maybeNull);
        }

        // If the value is one well-formed json literal: null, a number, or a boolean; attempt to deserialize the string then serialize to node,
        // otherwise as well as on failure pass the type as a string
        var reserialized = ReserializeNode(value, typeInfo);
        var nodeKind = TypeCache.KindFromNode(reserialized, value);
        if ((serializeAsKind | nodeKind) != serializeAsKind)
        {
            _typeCache.AddSerializeAsKind(typeInfo, nodeKind);
        }

        return reserialized ?? CreateStringNode(value, maybeNull);

        static object? DeserializeUnsafe(ReadOnlySpan<char> value, JsonTypeInfo typeInfo)
        {
            var maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
            var pooled = maxByteCount > JsonConstants.StackallocByteLimit
                ? ArrayPool<byte>.Shared.Rent(maxByteCount)
                : null;
            var bytes = pooled ?? stackalloc byte[maxByteCount];
            bytes = bytes[..Encoding.UTF8.GetBytes(value, bytes)];
            Utf8JsonReader reader = new(bytes);
            var result = JsonSerializer.Deserialize(ref reader, typeInfo);
            if (pooled != null)
            {
                ArrayPool<byte>.Shared.Return(pooled);
            }

            return result;
        }

        static JsonNode? ReserializeNode(ReadOnlySpan<char> s, JsonTypeInfo jsonTypeInfo)
        {
            try
            {
                var boxed = DeserializeUnsafe(s, jsonTypeInfo);
                return JsonSerializer.SerializeToNode(boxed, jsonTypeInfo);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    internal JsonNode? CreateStringNode(ReadOnlySpan<char> value, bool maybeNullLiteral = false)
    {
        return maybeNullLiteral && JsonConstants.IsNullLiteral(value) ? null : JsonValue.Create(value.ToString(), NodeOptions);
    }

    private JsonValue? CreateNumberNode(ReadOnlySpan<char> value)
    {
        // Guards against named literals Infinity, -Infinity, NaN
        if (!ValueJsonNumber.TryParse(value, out var jsonNumber))
        {
            return null;
        }

        if (jsonNumber.MaybeInt64
            && long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var longValue
            ))
        {
            return JsonValue.Create(longValue, NodeOptions);
        }

        if (jsonNumber.MaybeUInt64
            && ulong.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var ulongValue
            ))
        {
            return JsonValue.Create(ulongValue, NodeOptions);
        }

        if (jsonNumber.MaybeExactDecimal && decimal.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var decimalValue
            ))
        {
            return JsonValue.Create(decimalValue, NodeOptions);
        }

        // if the backing ITypeInfoResolver does not support JsonNumber, fallback to BigInteger & double
        // this might be the case when using JsonSourceContext
        if (options.GetTypeInfo(typeof(JsonNumber)) is JsonTypeInfo<JsonNumber> jsonNumberType)
        {
            return JsonValue.Create(
                jsonNumber.ToJsonNumber(),
                jsonNumberType,
                NodeOptions
            );
        }

        if (jsonNumber.IsInteger && options.GetTypeInfo(typeof(BigInteger)) is JsonTypeInfo<BigInteger> bigIntegerType)
        {
            if (BigInteger.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var bigIntegerValue
                ))
            {
                return JsonValue.Create(bigIntegerValue, bigIntegerType, NodeOptions);
            }
        }

        if (double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var doubleValue
            ))
        {
            return JsonValue.Create(doubleValue, NodeOptions);
        }

        return null;
    }

    private JsonValue? CreateBooleanNode(ReadOnlySpan<char> value)
    {
        if (value.Equals("true", StringComparison.Ordinal))
        {
            return JsonValue.Create(true, NodeOptions);
        }

        if (value.Equals("false", StringComparison.Ordinal))
        {
            return JsonValue.Create(false, NodeOptions);
        }

        return null;
    }

    public JsonTypeInfo GetTypeInfo(Type type)
    {
        return options.GetTypeInfo(type);
    }

    internal JsonPropertyInfo? FindPropertyEscaped(JsonTypeInfo typeInfo, ReadOnlySpan<char> escapedPropertyName)
    {
#if NET9_0_OR_GREATER
        var pooled = escapedPropertyName.Length > JsonConstants.StackallocCharLimit
            ? ArrayPool<char>.Shared.Rent(escapedPropertyName.Length)
            : null;
        var chars = pooled ?? stackalloc char[escapedPropertyName.Length];
        var written = UriSpan.UnescapeDataString(escapedPropertyName, chars);
        var propertyName = written >= 0 ? chars[..written] : escapedPropertyName;
        var propertyInfo = _typeCache.FindProperty(
            typeInfo,
            propertyName
        );
        if (pooled != null)
        {
            ArrayPool<char>.Shared.Return(pooled);
        }

        return propertyInfo;
#else
        return _typeCache.FindProperty(typeInfo, UriSpan.UnescapeDataString(escapedPropertyName));
#endif
    }

    internal JsonPropertyInfo? FindProperty(JsonTypeInfo typeInfo, string propertyName)
    {
        return _typeCache.FindProperty(typeInfo, propertyName);
    }

    private static TypeCache GetOrCreateTypeCache(JsonSerializerOptions options)
    {
        return s_typeCacheByOptions.GetValue(options, static _ => new([], []));
    }

    private static JsonNodeOptions GetNodeOptions(JsonSerializerOptions options)
    {
        return new() { PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive, };
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

    internal void ThrowIfMaxDepthExceeded(QueryPath trace)
    {
        var maxDepth = options.MaxDepth == 0 ? 64 : options.MaxDepth;
        if (trace.Depth > maxDepth)
        {
            ThrowHelper.ThrowMaxDepthExceededException(trace);
        }
    }
}

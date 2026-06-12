using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using UrlEncodedToJson.Serialization;

namespace UrlEncodedToJson;

[Flags]
internal enum SerializeAsKind : byte
{
    Number = 1 << 0,
    Boolean = 1 << 1,
    String = 1 << 2,
    Null = 1 << 3
}

internal partial struct UrlEncodedElementConverter
{
    private sealed class TypeCache(ConcurrentDictionary<JsonTypeInfo, Dictionary<string, JsonPropertyInfo>> propertyInfoByTypeInfo, ConcurrentDictionary<JsonTypeInfo, SerializeAsKind> serializeAsKindByTypeInfo)
    {
        public JsonPropertyInfo? FindProperty(JsonTypeInfo typeInfo, string propertyName)
        {
            var dict = GetTypeProperties(typeInfo);
            return dict.TryGetValue(propertyName, out var result) ? result : null;
        }

#if NET9_0_OR_GREATER
        public JsonPropertyInfo? FindProperty(JsonTypeInfo typeInfo, ReadOnlySpan<char> propertyName)
        {
            var dict = GetTypeProperties(typeInfo);
            return dict.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(propertyName, out var result) ? result : null;
        }
#endif

        private Dictionary<string, JsonPropertyInfo> GetTypeProperties(JsonTypeInfo typeInfo)
        {
            var dict = propertyInfoByTypeInfo.GetOrAdd(typeInfo, static t =>
                t.Properties.Aggregate(
                    new Dictionary<string, JsonPropertyInfo>(t.Properties.Count,
                        t.Options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal),
                    static (dict, item) =>
                    {
                        dict[item.Name] = item;
                        return dict;
                    }
                ));
            return dict;
        }

        public SerializeAsKind CanSerializeAsKind(JsonTypeInfo typeInfo)
        {
            return serializeAsKindByTypeInfo.TryGetValue(typeInfo, out var r) ? r : default;
        }

        public static SerializeAsKind KindFromNode(JsonNode? node, ReadOnlySpan<char> rawText)
        {
            return node?.GetValueKind() switch
            {
                null => SerializeAsKind.Null,
                JsonValueKind.String => JsonConstants.IsNamedFloatingPointLiteral(rawText) ? default : SerializeAsKind.String,
                JsonValueKind.Number => SerializeAsKind.Number,
                JsonValueKind.True or JsonValueKind.False => SerializeAsKind.Boolean,
                _ => default,
            };
        }

        public void AddSerializeAsKind(JsonTypeInfo typeInfo, SerializeAsKind v)
        {
            serializeAsKindByTypeInfo.AddOrUpdate(typeInfo, static (_, v) => v, static (_, e, v) => e | v, v);
        }
    }
}

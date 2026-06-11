using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

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
            return dict.GetValueOrDefault(propertyName);
        }

        public SerializeAsKind CanSerializeAsKind(JsonTypeInfo typeInfo)
        {
            return serializeAsKindByTypeInfo.TryGetValue(typeInfo, out var r) ? r : default;
        }

        public static SerializeAsKind KindFromNode(JsonNode? node)
        {
            return node?.GetValueKind() switch
            {
                null => SerializeAsKind.Null,
                JsonValueKind.String => SerializeAsKind.String,
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

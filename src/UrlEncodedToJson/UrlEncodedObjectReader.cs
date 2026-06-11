using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace UrlEncodedToJson;

internal readonly ref struct UrlEncodedObjectReader(UrlEncodElementConverter converter, JsonObject obj, JsonTypeInfo typeInfo, NestingTrace trace)
{
    public void AddObjectValue(
        ReadOnlySpan<char> path, string value)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        var (escapedPropertyName, remainingPath) = UrlEncodElementConverter.TakeFromPath(path);

        if (escapedPropertyName.IsEmpty)
        {
            return;
        }

        var propertyInfo = converter.FindProperty(typeInfo, UrlEncodElementConverter.UnescapeQueryComponent(escapedPropertyName));

        if (propertyInfo is null)
        {
            return;
        }

        var propertyTypeInfo = converter.GetTypeInfo(propertyInfo.PropertyType);

        if (remainingPath.IsEmpty)
        {
            UrlEncodedObjectReader reader = new(converter, obj, propertyTypeInfo, trace);
            reader.AddLeafValue(propertyInfo.Name, value);
            return;
        }

        switch (propertyTypeInfo.Kind)
        {
            case JsonTypeInfoKind.Object:
                CreateObjectReader(propertyInfo.Name, propertyTypeInfo).AddObjectValue(remainingPath, value);
                break;
            case JsonTypeInfoKind.Dictionary:
                CreateDictionaryReader(propertyInfo.Name, propertyTypeInfo).AddDictionaryValue(remainingPath, value);
                break;
            case JsonTypeInfoKind.Enumerable:
                CreateArrayReader(propertyInfo.Name, propertyTypeInfo).AddArrayValue(remainingPath, value);
                break;
            case JsonTypeInfoKind.None:
            default:
                CreateLeafReader(propertyInfo.Name, propertyTypeInfo).AddLeafValue(propertyInfo.Name, value);
                break;
        }
    }

    public void AddDictionaryValue(ReadOnlySpan<char> path, string value)
    {
        var (escapedKey, childPath) = UrlEncodElementConverter.TakeFromPath(path);
        var key = UrlEncodElementConverter.UnescapeQueryComponent(escapedKey);
        if (childPath.IsEmpty)
        {
            AddLeafValue(key, value);
            return;
        }

        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Object:
                CreateObjectReader(key, typeInfo).AddObjectValue(childPath, value);
                break;
            case JsonTypeInfoKind.Dictionary:
                CreateDictionaryReader(key, typeInfo).AddDictionaryValue(childPath, value);
                break;
            case JsonTypeInfoKind.Enumerable:
                CreateArrayReader(key, typeInfo).AddArrayValue(childPath, value);
                break;
            case JsonTypeInfoKind.None:
            default:
                AddLeafValue(key, value);
                break;
        }
    }

    private void AddLeafValue(
        string key,
        string value
    )
    {
        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Enumerable:
                GetOrCreateArray(key).Add(value);
                return;
            case JsonTypeInfoKind.None:
                obj[key] = converter.StringToValue(value, typeInfo);
                return;
            case JsonTypeInfoKind.Object:
            case JsonTypeInfoKind.Dictionary:
            default:
                UrlEncodElementConverter.ThrowInvalidLeafTypeException(trace[key], value, typeInfo);
                return;
        }
    }

    private UrlEncodedArrayReader CreateArrayReader(string key, JsonTypeInfo ownerTypeInfo)
    {
        var t = trace[key];
        return new(
            converter,
            GetOrCreateArray(key),
            converter.GetElementTypeInfo(ownerTypeInfo, t),
            t
        );
    }

    private UrlEncodedObjectReader CreateObjectReader(string key, JsonTypeInfo ownerTypeInfo)
    {
        var t = trace[key];
        return new(
            converter,
            GetOrCreateObject(key),
            ownerTypeInfo,
            t
        );
    }

    private UrlEncodedObjectReader CreateDictionaryReader(string key, JsonTypeInfo ownerTypeInfo)
    {
        var t = trace[key];
        return new(
            converter,
            GetOrCreateObject(key),
            converter.GetElementTypeInfo(ownerTypeInfo, t),
            t
        );
    }

    private UrlEncodedObjectReader CreateLeafReader(string key, JsonTypeInfo ownerTypeInfo)
    {
        var t = trace[key];
        return new(
            converter,
            obj,
            converter.GetElementTypeInfo(ownerTypeInfo, t),
            t
        );
    }

    private JsonObject GetOrCreateObject(string key)
    {
        if (obj[key] is JsonObject child)
        {
            return child;
        }

        child = new(converter.NodeOptions);
        obj[key] = child;

        return child;
    }

    private JsonArray GetOrCreateArray(string key)
    {
        if (obj[key] is JsonArray child)
        {
            return child;
        }

        child = new(converter.NodeOptions);
        obj[key] = child;

        return child;
    }

    public JsonObject ToJsonObject()
    {
        return obj;
    }
}

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace UrlEncodedToJson;

public static partial class UrlEncodedSerializer
{
    /// <summary>
    /// Parse the provided JSON and deserialize it to a URL-encoded query string using the contract for <typeparamref name="T"/>
    /// from the specified <see cref="JsonSerializerContext"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing the JSON element.</typeparam>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <param name="context">The source-generated serializer context providing type metadata.</param>
    /// <returns>The URL-encoded string equivalent to the JSON if any; otherwise <c>null</c>.</returns>
    public static string? Deserialize<T>(ReadOnlySpan<char> json, JsonSerializerContext context)
    {
        return new UrlEncodedElementConverter(context.Options).Deserialize<T>(json);
    }

    /// <summary>
    /// Parse the provided JSON and deserialize it to a URL-encoded query string using the specified <paramref name="type"/>
    /// from the provided <see cref="JsonSerializerContext"/>.
    /// </summary>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <param name="type">The contract type to use when deserializing.</param>
    /// <param name="context">The source-generated serializer context providing type metadata.</param>
    /// <returns>The URL-encoded string equivalent to the JSON if any; otherwise <c>null</c>.</returns>
    public static string? Deserialize(ReadOnlySpan<char> json, Type type, JsonSerializerContext context)
    {
        return new UrlEncodedElementConverter(context.Options).Deserialize(json, type);
    }

    /// <summary>
    /// Parse the provided JSON and deserialize it to URL-encoded bytes using the contract for <typeparamref name="T"/>
    /// from the provided <see cref="JsonSerializerContext"/>, writing the result to <paramref name="writer"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing.</typeparam>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <param name="writer">The destination buffer writer for URL-encoded bytes.</param>
    /// <param name="context">The source-generated serializer context providing type metadata.</param>
    public static void Deserialize<T>(
        ReadOnlySpan<char> json,
        IBufferWriter<byte> writer,
        JsonSerializerContext context
    )
    {
        new UrlEncodedElementConverter(context.Options).Deserialize<T>(json, writer);
    }

    /// <summary>
    /// Parse the provided JSON and deserialize it to URL-encoded bytes using the specified <paramref name="type"/>
    /// from the provided <see cref="JsonSerializerContext"/>, writing the result to <paramref name="writer"/>.
    /// </summary>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <param name="type">The contract type to use when deserializing.</param>
    /// <param name="writer">The destination buffer writer for URL-encoded bytes.</param>
    /// <param name="context">The source-generated serializer context providing type metadata.</param>
    public static void Deserialize(
        ReadOnlySpan<char> json,
        Type type,
        IBufferWriter<byte> writer,
        JsonSerializerContext context
    )
    {
        new UrlEncodedElementConverter(context.Options).Deserialize(json, type, writer);
    }

    /// <summary>
    /// Deserialize the URL-encoded data from the provided <see cref="JsonElement"/> using the contract for <typeparamref name="T"/>
    /// from the provided <see cref="JsonSerializerContext"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing the JSON element.</typeparam>
    /// <param name="element">The JSON element containing the URL-encoded data.</param>
    /// <param name="context">The source-generated serializer context providing type metadata.</param>
    /// <returns>The URL-encoded string equivalent to the element if any; otherwise <c>null</c>.</returns>
    public static string? Deserialize<T>(JsonElement element, JsonSerializerContext context)
    {
        return new UrlEncodedElementConverter(context.Options).Deserialize<T>(element);
    }

    /// <summary>
    /// Deserialize the URL-encoded data from the provided <see cref="JsonElement"/> using the specified <paramref name="type"/>
    /// from the provided <see cref="JsonSerializerContext"/>.
    /// </summary>
    /// <param name="element">The JSON element containing the URL-encoded data.</param>
    /// <param name="type">The contract type to use when deserializing.</param>
    /// <param name="context">The source-generated serializer context providing type metadata.</param>
    /// <returns>The URL-encoded string equivalent to the element if any; otherwise <c>null</c>.</returns>
    public static string? Deserialize(JsonElement element, Type type, JsonSerializerContext context)
    {
        return new UrlEncodedElementConverter(context.Options).Deserialize(element, type);
    }

    /// <summary>
    /// Deserialize the URL-encoded data from the provided <see cref="JsonElement"/> using the contract for <typeparamref name="T"/>
    /// from the provided <see cref="JsonSerializerContext"/> and write the result to <paramref name="writer"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing.</typeparam>
    /// <param name="element">The JSON element containing the URL-encoded data.</param>
    /// <param name="writer">The destination buffer writer.</param>
    /// <param name="context">The source-generated serializer context providing type metadata.</param>
    public static void Deserialize<T>(JsonElement element, IBufferWriter<byte> writer, JsonSerializerContext context)
    {
        new UrlEncodedElementConverter(context.Options).Deserialize<T>(element, writer);
    }

    /// <summary>
    /// Deserialize the URL-encoded data from the provided <see cref="JsonElement"/> using the specified <paramref name="type"/>
    /// from the provided <see cref="JsonSerializerContext"/> and write the result to <paramref name="writer"/>.
    /// </summary>
    /// <param name="element">The JSON element containing the URL-encoded data.</param>
    /// <param name="type">The contract type to use when deserializing.</param>
    /// <param name="writer">The destination buffer writer.</param>
    /// <param name="context">The source-generated serializer context providing type metadata.</param>
    public static void Deserialize(
        JsonElement element,
        Type type,
        IBufferWriter<byte> writer,
        JsonSerializerContext context
    )
    {
        new UrlEncodedElementConverter(context.Options).Deserialize(element, type, writer);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into JSON using the provided <see cref="JsonTypeInfo"/>
    /// and write it to <paramref name="writer"/>.
    /// </summary>
    public static void Serialize(ReadOnlySpan<char> query, JsonTypeInfo typeInfo, Utf8JsonWriter writer)
    {
        new UrlEncodedElementConverter(typeInfo.Options).Serialize(query, typeInfo, writer);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into JSON using the specified <paramref name="type"/>
    /// from the provided <see cref="JsonSerializerContext"/> and write it to <paramref name="writer"/>.
    /// </summary>
    public static void Serialize(
        ReadOnlySpan<char> query,
        Type type,
        Utf8JsonWriter writer,
        JsonSerializerContext context
    )
    {
        new UrlEncodedElementConverter(context.Options).Serialize(query, type, writer);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into JSON using the contract for <typeparamref name="T"/>
    /// from the provided <see cref="JsonSerializerContext"/> and write it to <paramref name="writer"/>.
    /// </summary>
    public static void Serialize<T>(ReadOnlySpan<char> query, Utf8JsonWriter writer, JsonSerializerContext context)
    {
        new UrlEncodedElementConverter(context.Options).Serialize<T>(query, writer);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a JSON string using the provided <see cref="JsonTypeInfo"/>.
    /// </summary>
    public static string? Serialize(ReadOnlySpan<char> query, JsonTypeInfo typeInfo)
    {
        return new UrlEncodedElementConverter(typeInfo.Options).Serialize(query, typeInfo);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a JSON string using the specified <paramref name="type"/>
    /// from the provided <see cref="JsonSerializerContext"/>.
    /// </summary>
    public static string? Serialize(ReadOnlySpan<char> query, Type type, JsonSerializerContext context)
    {
        return new UrlEncodedElementConverter(context.Options).Serialize(query, type);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a JSON string using the contract for <typeparamref name="T"/>
    /// from the provided <see cref="JsonSerializerContext"/>.
    /// </summary>
    public static string? Serialize<T>(ReadOnlySpan<char> query, JsonSerializerContext context)
    {
        return new UrlEncodedElementConverter(context.Options).Serialize<T>(query);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a <see cref="JsonNode"/> using the contract for <typeparamref name="T"/>
    /// from the provided <see cref="JsonSerializerContext"/>.
    /// </summary>
    public static JsonNode? SerializeToNode<T>(ReadOnlySpan<char> query, JsonSerializerContext context)
    {
        return new UrlEncodedElementConverter(context.Options).SerializeToNode<T>(query);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a <see cref="JsonNode"/> using the specified <paramref name="type"/>
    /// from the provided <see cref="JsonSerializerContext"/>.
    /// </summary>
    public static JsonNode? SerializeToNode(ReadOnlySpan<char> query, Type type, JsonSerializerContext context)
    {
        return new UrlEncodedElementConverter(context.Options).SerializeToNode(
            query,
            context.GetTypeInfo(type)
            ?? throw new JsonException("The provided JsonSerializerContext does not support the type.")
            {
                Data = { ["Type"] = type }
            }
        );
    }
}

using System.Buffers;
using System.Diagnostics.Contracts;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace UrlEncodedToJson;

public partial class UrlEncodedSerializer
{
    /// <summary>
    /// Deserialize the URL-encoded element contained in the provided JSON using the contract for <typeparamref name="T"/>.
    /// The JSON is parsed into a <see cref="JsonElement"/> using the converter's document options before deserialization.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing the JSON element.</typeparam>
    /// <param name="converter">The converter instance.</param>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <returns>The URL-encoded string if any; otherwise <c>null</c>.</returns>
    [Pure]
    internal static string? Deserialize<T>(this UrlEncodedElementConverter converter, ReadOnlySpan<char> json)
    {
        return converter.Deserialize<T>(ParseElement(converter, json));
    }

    /// <summary>
    /// Deserialize the URL-encoded element contained in the provided JSON using the specified <paramref name="type"/> contract.
    /// The JSON is parsed into a <see cref="JsonElement"/> using the converter's document options before deserialization.
    /// </summary>
    /// <param name="converter">The converter instance.</param>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <param name="type">The contract type to use when deserializing.</param>
    /// <returns>The URL-encoded string if any; otherwise <c>null</c>.</returns>
    [Pure]
    internal static string? Deserialize(this UrlEncodedElementConverter converter, ReadOnlySpan<char> json, Type type)
    {
        return converter.Deserialize(ParseElement(converter, json), type);
    }

    /// <summary>
    /// Deserialize the URL-encoded element contained in the provided JSON using the contract for <typeparamref name="T"/>
    /// and write the URL-encoded bytes to <paramref name="writer"/>.
    /// The JSON is parsed into a <see cref="JsonElement"/> using the converter's document options before deserialization.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing.</typeparam>
    /// <param name="converter">The converter instance.</param>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <param name="writer">The destination buffer writer for URL-encoded bytes.</param>
    internal static void Deserialize<T>(this UrlEncodedElementConverter converter, ReadOnlySpan<char> json, IBufferWriter<byte> writer)
    {
        converter.Deserialize<T>(ParseElement(converter, json), writer);
    }

    /// <summary>
    /// Deserialize the URL-encoded element from the JSON using the <paramref name="type"/> contract.
    /// The JSON is parsed into a <see cref="JsonElement"/> using the converter's document options before deserialization.
    /// </summary>
    /// <param name="converter">The converter instance.</param>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <param name="type">The contract type.</param>
    /// <param name="writer">The URL-encoded output writer.</param>
    internal static void Deserialize(this UrlEncodedElementConverter converter, ReadOnlySpan<char> json, Type type, IBufferWriter<byte> writer)
    {
        converter.Deserialize(ParseElement(converter, json), type, writer);
    }

    /// <summary>
    /// Deserialize the URL-encoded element from the provided <see cref="JsonElement"/> using the contract for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing the JSON element.</typeparam>
    /// <param name="converter">The converter instance.</param>
    /// <param name="element">The JSON element containing the URL-encoded element.</param>
    /// <returns>The URL-encoded string if any; otherwise <c>null</c>.</returns>
    [Pure]
    internal static string? Deserialize<T>(this UrlEncodedElementConverter converter, JsonElement element)
    {
        return converter.Deserialize(element, typeof(T));
    }

    /// <summary>
    /// Deserialize the URL-encoded element from the provided <see cref="JsonElement"/> using the specified <paramref name="type"/> contract.
    /// </summary>
    /// <param name="converter">The converter instance.</param>
    /// <param name="element">The JSON element.</param>
    /// <param name="type">The contract type.</param>
    /// <returns>The URL-encoded element equivalent to the <paramref name="element"/>.</returns>
    [Pure]
    internal static string? Deserialize(this UrlEncodedElementConverter converter, JsonElement element, Type type)
    {
        return converter.Deserialize(element, converter.GetTypeInfo(type));
    }

    /// <summary>
    /// Deserialize the URL-encoded element from the provided <see cref="JsonElement"/> using the contract for <typeparamref name="T"/>
    /// and write the result to <paramref name="writer"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing the JSON element.</typeparam>
    /// <param name="converter">The converter instance.</param>
    /// <param name="element">The JSON element containing the URL-encoded element.</param>
    /// <param name="writer">The URL-encoded output writer.</param>
    internal static void Deserialize<T>(this UrlEncodedElementConverter converter, JsonElement element, IBufferWriter<byte> writer)
    {
        converter.Deserialize(element, typeof(T), writer);
    }

    /// <summary>
    /// Deserialize the URL-encoded element from the provided <see cref="JsonElement"/> using the specified <paramref name="type"/> contract
    /// and write the result to <paramref name="writer"/>.
    /// </summary>
    /// <param name="converter">The converter instance.</param>
    /// <param name="element">The JSON element containing the URL-encoded element.</param>
    /// <param name="type">The contract type.</param>
    /// <param name="writer">The URL-encoded output writer.</param>
    internal static void Deserialize(this UrlEncodedElementConverter converter, JsonElement element, Type type, IBufferWriter<byte> writer)
    {
        converter.Deserialize(element, converter.GetTypeInfo(type), writer);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into JSON using the provided <paramref name="typeInfo"/> and write it to <paramref name="writer"/>.
    /// </summary>
    /// <param name="converter">The converter instance.</param>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <param name="typeInfo">The JSON contract information to use when serializing.</param>
    /// <param name="writer">The destination JSON writer.</param>
    internal static void Serialize(this UrlEncodedElementConverter converter, ReadOnlySpan<char> query, JsonTypeInfo typeInfo, Utf8JsonWriter writer)
    {
        var node = converter.SerializeToNode(query, typeInfo);
        if (node is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            node.WriteTo(writer);
        }
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into JSON using the specified <paramref name="type"/> contract and write it to <paramref name="writer"/>.
    /// </summary>
    /// <param name="converter">The converter instance.</param>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <param name="type">The contract type.</param>
    /// <param name="writer">The destination JSON writer.</param>
    internal static void Serialize(this UrlEncodedElementConverter converter, ReadOnlySpan<char> query, Type type, Utf8JsonWriter writer)
    {
        converter.Serialize(query, converter.GetTypeInfo(type), writer);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into JSON using the contract for <typeparamref name="T"/> and write it to <paramref name="writer"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when serializing the query.</typeparam>
    /// <param name="converter">The converter instance.</param>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <param name="writer">The destination JSON writer.</param>
    internal static void Serialize<T>(this UrlEncodedElementConverter converter, ReadOnlySpan<char> query, Utf8JsonWriter writer)
    {
        converter.Serialize(query, typeof(T), writer);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a JSON string using the provided <paramref name="typeInfo"/>.
    /// </summary>
    /// <param name="converter">The converter instance.</param>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <param name="typeInfo">The JSON contract information to use when serializing.</param>
    /// <returns>The serialized JSON string if any; otherwise <c>null</c>.</returns>
    [Pure]
    internal static string? Serialize(this UrlEncodedElementConverter converter, ReadOnlySpan<char> query, JsonTypeInfo typeInfo)
    {
        return converter.SerializeToNode(query, typeInfo)?.ToJsonString();
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a JSON string using the specified <paramref name="type"/> contract.
    /// </summary>
    /// <param name="converter">The converter instance.</param>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <param name="type">The contract type.</param>
    /// <returns>The serialized JSON string if any; otherwise <c>null</c>.</returns>
    [Pure]
    internal static string? Serialize(this UrlEncodedElementConverter converter, ReadOnlySpan<char> query, Type type)
    {
        return converter.Serialize(query, converter.GetTypeInfo(type));
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a JSON string using the contract for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when serializing the query.</typeparam>
    /// <param name="converter">The converter instance.</param>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <returns>The serialized JSON string if any; otherwise <c>null</c>.</returns>
    [Pure]
    internal static string? Serialize<T>(this UrlEncodedElementConverter converter, ReadOnlySpan<char> query)
    {
        return converter.Serialize(query, typeof(T));
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a <see cref="JsonNode"/> using the contract for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when serializing the query.</typeparam>
    /// <param name="converter">The converter instance.</param>
    /// <param name="query">The URL-encoded query text to convert to a <see cref="JsonNode"/>.</param>
    /// <returns>The resulting <see cref="JsonNode"/> if any; otherwise <c>null</c>.</returns>
    internal static JsonNode? SerializeToNode<T>(this UrlEncodedElementConverter converter, ReadOnlySpan<char> query)
    {
        return converter.SerializeToNode(query, typeof(T));
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a <see cref="JsonNode"/> using the specified <paramref name="type"/> contract.
    /// </summary>
    /// <param name="converter">The converter instance.</param>
    /// <param name="query">The URL-encoded query text to convert to a <see cref="JsonNode"/>.</param>
    /// <param name="type">The contract type.</param>
    /// <returns>The resulting <see cref="JsonNode"/> if any; otherwise <c>null</c>.</returns>
    internal static JsonNode? SerializeToNode(this UrlEncodedElementConverter converter, ReadOnlySpan<char> query, Type type)
    {
        return converter.SerializeToNode(query, converter.GetTypeInfo(type));
    }

    /// <summary>
    /// Parse the provided JSON into a <see cref="JsonElement"/> using the converter's document options.
    /// </summary>
    /// <param name="converter">The converter instance whose document options will be used for parsing.</param>
    /// <param name="json">The JSON to parse.</param>
    /// <returns>The parsed <see cref="JsonElement"/>.</returns>
    private static JsonElement ParseElement(UrlEncodedElementConverter converter, ReadOnlySpan<char> json)
    {
        return JsonElement.Parse(json, converter.DocumentOptions);
    }
}

using System.Diagnostics.Contracts;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Buffers;
using System.Text.Json.Serialization.Metadata;
using System.Diagnostics.CodeAnalysis;

namespace UrlEncodedToJson;

/// <summary>
/// Converts elements between URL-encoded and JSON using type serialization metadata provided by <see cref="JsonSerializerOptions"/>.
/// </summary>
public static partial class UrlEncodedSerializer
{
    /// <summary>
    /// Parse the provided JSON and deserialize it to a URL-encoded query string using the contract for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing the JSON element.</typeparam>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    /// <returns>The URL-encoded string equivalent to the JSON if any; otherwise <c>null</c>.</returns>
    [Pure]
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static string? Deserialize<T>(ReadOnlySpan<char> json, JsonSerializerOptions? options = null)
    {
        return ConverterForOption(options).Deserialize<T>(json);
    }

    /// <summary>
    /// Parse the provided JSON and deserialize it to a URL-encoded query string using the specified <paramref name="type"/> contract.
    /// </summary>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <param name="type">The contract type to use when deserializing.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    /// <returns>The URL-encoded string equivalent to the JSON if any; otherwise <c>null</c>.</returns>
    [Pure]
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static string? Deserialize(ReadOnlySpan<char> json, Type type, JsonSerializerOptions? options = null)
    {
        return ConverterForOption(options).Deserialize(json, type);
    }

    /// <summary>
    /// Parse the provided JSON and deserialize it to URL-encoded bytes using the contract for <typeparamref name="T"/>,
    /// writing the result to <paramref name="writer"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing.</typeparam>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <param name="writer">The destination buffer writer for URL-encoded bytes.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static void Deserialize<T>(ReadOnlySpan<char> json, IBufferWriter<byte> writer, JsonSerializerOptions? options = null)
    {
        ConverterForOption(options).Deserialize<T>(json, writer);
    }

    /// <summary>
    /// Parse the provided JSON and deserialize it to URL-encoded bytes using the specified <paramref name="type"/> contract,
    /// writing the result to <paramref name="writer"/>.
    /// </summary>
    /// <param name="json">The JSON to parse and deserialize.</param>
    /// <param name="type">The contract type to use when deserializing.</param>
    /// <param name="writer">The destination buffer writer for URL-encoded bytes.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static void Deserialize(ReadOnlySpan<char> json, Type type, IBufferWriter<byte> writer, JsonSerializerOptions? options = null)
    {
        ConverterForOption(options).Deserialize(json, type, writer);
    }

    /// <summary>
    /// Deserialize the URL-encoded data from the provided <see cref="JsonElement"/> using the contract for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing the JSON element.</typeparam>
    /// <param name="element">The JSON element containing the URL-encoded data.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    /// <returns>The URL-encoded string equivalent to the <paramref name="element"/> if any; otherwise <c>null</c>.</returns>
    [Pure]
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static string? Deserialize<T>(JsonElement element, JsonSerializerOptions? options = null)
    {
        return ConverterForOption(options).Deserialize<T>(element);
    }

    /// <summary>
    /// Deserialize the URL-encoded data from the provided <see cref="JsonElement"/> using the specified <paramref name="type"/> contract.
    /// </summary>
    /// <param name="element">The JSON element containing the URL-encoded data.</param>
    /// <param name="type">The contract type to use when deserializing.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    /// <returns>The URL-encoded string equivalent to the <paramref name="element"/> if any; otherwise <c>null</c>.</returns>
    [Pure]
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static string? Deserialize(JsonElement element, Type type, JsonSerializerOptions? options = null)
    {
        return ConverterForOption(options).Deserialize(element, type);
    }

    /// <summary>
    /// Deserialize the URL-encoded data from the provided <see cref="JsonElement"/> using the contract for <typeparamref name="T"/>
    /// and write the result to <paramref name="writer"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when deserializing the JSON element.</typeparam>
    /// <param name="element">The JSON element containing the URL-encoded data.</param>
    /// <param name="writer">The destination buffer writer for URL-encoded bytes.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static void Deserialize<T>(JsonElement element, IBufferWriter<byte> writer, JsonSerializerOptions? options = null)
    {
        ConverterForOption(options).Deserialize<T>(element, writer);
    }

    /// <summary>
    /// Deserialize the URL-encoded data from the provided <see cref="JsonElement"/> using the specified <paramref name="type"/> contract
    /// and write the result to <paramref name="writer"/>.
    /// </summary>
    /// <param name="element">The JSON element containing the URL-encoded data.</param>
    /// <param name="type">The contract type to use when deserializing.</param>
    /// <param name="writer">The destination buffer writer for URL-encoded bytes.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static void Deserialize(JsonElement element, Type type, IBufferWriter<byte> writer, JsonSerializerOptions? options = null)
    {
        ConverterForOption(options).Deserialize(element, type, writer);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into JSON using the provided <paramref name="typeInfo"/> and write it to <paramref name="writer"/>.
    /// </summary>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <param name="typeInfo">The JSON contract information to use when serializing.</param>
    /// <param name="writer">The destination JSON writer.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static void Serialize(ReadOnlySpan<char> query, JsonTypeInfo typeInfo, Utf8JsonWriter writer, JsonSerializerOptions? options)
    {
        ConverterForOption(options).Serialize(query, typeInfo, writer);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into JSON using the specified <paramref name="type"/> contract and write it to <paramref name="writer"/>.
    /// </summary>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <param name="type">The contract type to use when serializing.</param>
    /// <param name="writer">The destination JSON writer.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static void Serialize(ReadOnlySpan<char> query, Type type, Utf8JsonWriter writer, JsonSerializerOptions? options = null)
    {
        ConverterForOption(options).Serialize(query, type, writer);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into JSON using the contract for <typeparamref name="T"/> and write it to <paramref name="writer"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when serializing the query.</typeparam>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <param name="writer">The destination JSON writer.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static void Serialize<T>(ReadOnlySpan<char> query, Utf8JsonWriter writer, JsonSerializerOptions? options = null)
    {
        ConverterForOption(options).Serialize<T>(query, writer);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a JSON string using the provided <paramref name="typeInfo"/>.
    /// </summary>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <param name="typeInfo">The JSON contract information to use when serializing.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    /// <returns>The serialized JSON string if any; otherwise <c>null</c>.</returns>
    [Pure]
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static string? Serialize(ReadOnlySpan<char> query, JsonTypeInfo typeInfo, JsonSerializerOptions? options)
    {
        return ConverterForOption(options).Serialize(query, typeInfo);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a JSON string using the specified <paramref name="type"/> contract.
    /// </summary>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <param name="type">The contract type to use when serializing.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    /// <returns>The serialized JSON string if any; otherwise <c>null</c>.</returns>
    [Pure]
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static string? Serialize(ReadOnlySpan<char> query, Type type, JsonSerializerOptions? options = null)
    {
        return ConverterForOption(options).Serialize(query, type);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a JSON string using the contract for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when serializing the query.</typeparam>
    /// <param name="query">The URL-encoded query text to convert to JSON.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    /// <returns>The serialized JSON string if any; otherwise <c>null</c>.</returns>
    [Pure]
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static string? Serialize<T>(ReadOnlySpan<char> query, JsonSerializerOptions? options = null)
    {
        return ConverterForOption(options).Serialize<T>(query);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a <see cref="JsonNode"/> using the contract for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The contract type to use when serializing the query.</typeparam>
    /// <param name="query">The URL-encoded query text to convert to a <see cref="JsonNode"/>.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    /// <returns>The resulting <see cref="JsonNode"/> if any; otherwise <c>null</c>.</returns>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static JsonNode? SerializeToNode<T>(ReadOnlySpan<char> query, JsonSerializerOptions? options = null)
    {
        return ConverterForOption(options).SerializeToNode<T>(query);
    }

    /// <summary>
    /// Serialize the URL-encoded <paramref name="query"/> into a <see cref="JsonNode"/> using the specified <paramref name="type"/> contract.
    /// </summary>
    /// <param name="query">The URL-encoded query text to convert to a <see cref="JsonNode"/>.</param>
    /// <param name="type">The contract type to use when serializing.</param>
    /// <param name="options">Optional serializer options used to obtain type serialization metadata. If <c>null</c>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
    /// <returns>The resulting <see cref="JsonNode"/> if any; otherwise <c>null</c>.</returns>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    public static JsonNode? SerializeToNode(ReadOnlySpan<char> query, Type type, JsonSerializerOptions? options = null)
    {
        return ConverterForOption(options).SerializeToNode(query, ConverterForOption(options).GetTypeInfo(type));
    }

    /// <summary>
    /// Create a <see cref="UrlEncodedElementConverter"/> for the provided <paramref name="options"/>, falling back to <see cref="JsonSerializerOptions.Default"/>.
    /// </summary>
    /// <param name="options">Optional serializer options.</param>
    /// <returns>A new <see cref="UrlEncodedElementConverter"/> configured with the provided options.</returns>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializerOptions.Default")]
    private static UrlEncodedElementConverter ConverterForOption(JsonSerializerOptions? options)
    {
        return new(options ?? JsonSerializerOptions.Default);
    }
}

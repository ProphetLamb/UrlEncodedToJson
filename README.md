[![NuGet Version](https://img.shields.io/nuget/v/UrlEncodedToJson)](https://www.nuget.org/packages/UrlEncodedToJson) [![NuGet Downloads](https://img.shields.io/nuget/dt/UrlEncodedToJson)](https://www.nuget.org/packages/UrlEncodedToJson)

```bash
dotnet add package UrlEncodedToJson
```

# URL-encoded to JSON

Converts elements between URL-encoded and JSON using type serialization metadata provided by `JsonSerializerOptions`.

- Very fast because it rewrites syntax instead of performing expensive reflection independent of `System.Text.Json`.
- Supports reflection-free, source generated `JsonSourceContext`.

```csharp
var query = string.Join('&', [
    "Items.0.Id=12",
    "Items.0.Name.First=Paul",
    "Items.0.Name.Last=Steiner",
    "Metadata.Customer.Name.First=Paul",
    "Metadata.Customer.MyInterests=MTB",
    "Metadata.Customer.MyInterests=HEMA",
    "Metadata.Customer.MyInterests=Golf",
    "Metadata.Created%2ETs=2026-06-09T16%3A41%3A12Z",
    "Matrix.0.1=12.45",
]);

var json = UrlEncodedSerializer.Serialize<C>(query);
var obj = JsonSerializer.Deserialize<C>(json ?? "");
// {"Items":[{"Id":12,"Name":{"First":"Paul","Last":"Steiner"}}],"Metadata":{"Customer":{"Name":{"First":"Paul"},"MyInterests":["MTB","HEMA","Golf"]},"Created.Ts":"2026-06-09T16:41:12Z"},"Matrix":[[null,12.45]]}
var queryRoundTrip = UrlEncodedSerializer.Deserialize<C>(json);

record C(IImmutableList<P> Items, M Metadata, List<List<double?>> Matrix);
record P(long Id, N Name, Age Age, IImmutableSet<string> MyInterests);
record N(string First, string Last);
record M(P Customer, [property: JsonPropertyName("Created.Ts")] DateTimeOffset CreatedTs);
[JsonConverter(typeof(AgeConverter))]
record Age(int Value);
class AgeConverter : JsonConverter<Age?>
{
    public override Age? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<int?>(ref reader, options) is {} v ? new(v) : null;
    }

    public override void Write(Utf8JsonWriter writer, Age value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value?.Value, options);
    }
}
```

## Format specifics

### Dot handling

All dots (`.`) in JSON property names are escaped using the URL-escape-sequence `%2E`. The property is unescaped again when deserializing so that correct JSON is generated.

```txt
Created.Ts -> Created%2ETs
```

Note that the `JsonNamingPolicy` of the `options` applies before escaping.

### Space escaping

An escape space (`+`) in the URL-encoded data is unescaped to space (` `). This allows broader compatibility.

## Enumerable types

Enumerable syntax supports either index based placement, or appending.

### Enumerable appending

Simple value enumerables allow appending elements, by passing the same path multiple times.

For example: `MyInterests` is filled with all three values `MTB`, `HEMA`, `Golf`

```
> MyInterests=MTB
> MyInterests=HEMA
> MyInterests=Golf
MyInterests: [
    "MTB",
    "HEMA",
    "Golf",
]
```

### Enumerable index placement

Enumerable complex elements must use index placement, to determine ownership of properties.

Example: The query `Items.0.Id=12` asigns the value `12` to the `Id` field of the index `0` of `Items`.
Assigning at an existing index overwrites the value.

```
> Items.0.Id=12
Items: [
    { Id: 12 }
]

> Items.0.Id=11
Items: [
    { Id: 11 }
]
```

Example: The query `Matrix.0.1=12.45` assigns the value `12.45` to the first row and second column. To assign the second column, the first column is generated with a `null` value.

```
> Matrix.0.1=12.45
[
    [null, 12.45]
]
```


## Simple values

Primitive types are always read from their JSON tokens:

- Numbers
- Strings
- Booleans
- Null

However, if the value is no primitive, and syntax allows for both a stirng and a number, boolean, or null, then metadata is unable to determine which JSON element to use.

### Custom converters

Oftentimes custom converters obiously require a text token, such as for `DateTimeOffset`. This value could not possibly be a number, boolean, or null. In this case the text token is generated.

```
> CreatedAt=2026-06-09T16%3A41%3A12Z
```

Conflicting text that may require converter execution are `true`, `false`, `null`, and any number. After all the a number might be text, that just happened to look like a number.

### Strongly typed primitives

Strongly typed primitives are impossible to statically analyze, because oftentimes the syntax and data are indistinguishable from a simple value.

The following strongly typed primitive is serialized as a number. However no metadata determines wether `JsonTokenType` `AgeConverter` expects a string, a boolean, or a number.

To learn this information the converter deserializes and boxes the strongly typed primitive at most once across all runs. The custom converter must behave predicably.

In this case the simple value deserialization is attempted as a number.
- If that fails, the converter catches the `JsonException`, and the simple value is treated as a string.
- If that succeeds, the converter remembers the type can convert from a number, and always handles number as number tokens instead of text.

```
> Person.Age=12
> Person.Age=null
```

1. Here the converter learns, to handle `12` as a number, instead of text.
2. Then the converter learns, to handle `null` as `null`, instead of text.

From this point on the `AgeConverter` is no longer queried:
- When encountering a quey value that can be interpreted as a number, then a number JSON token is generated.
- When encountering a quey value that can be interpreted as null, then the null JSON token is generated.

```csharp
[JsonConverter(typeof(AgeConverter))]
record Age(int Value);
class AgeConverter : JsonConverter<Age>
{
    public override Age? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new(JsonSerializer.Deserialize<int>(ref reader, options));
    }

    public override void Write(Utf8JsonWriter writer, Age value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Value, options);
    }
}
```
#pragma warning disable CA2263 // Prefer generic overload when type is known
using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrlEncodedToJson.Tests;

public class RoundTripTests
{
    private readonly C _c = new(
        [new(12, new("Paul", "Steiner"), null!, [])],
        new(
            new(0, new("Paul", null!), null!, ["MTB", "HEMA", "Golf"]),
            DateTimeOffset.Parse("2026-06-09T16:41:12Z", CultureInfo.InvariantCulture)
        ),
        [[null, 12.45]]
    );

    private readonly string _query = string.Join('&', [
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

    [Test]
    public void TestGenericRoundTrip()
    {
        var json = UrlEncodedSerializer.Serialize<C>(_query);
        var obj = JsonSerializer.Deserialize<C>(json ?? "");
        AssertEqualsC(obj);
        var queryRoundTrip = UrlEncodedSerializer.Deserialize<C>(json);
        Assert.That(queryRoundTrip, Is.EqualTo(_query));
    }

    [Test]
    public void TestSpanDeserializeOverloads()
    {
        // Create JSON from query using Serialize<T>
        var json = UrlEncodedSerializer.Serialize<C>(_query.AsSpan());
        Assert.That(json, Is.Not.Null);

        // 1) Deserialize(ReadOnlySpan<char>, Type)
        var q1 = UrlEncodedSerializer.Deserialize(json.AsSpan(), typeof(C));
        Assert.That(q1, Is.EqualTo(_query));

        // 2) Deserialize<T>(ReadOnlySpan<char>, writer)
        var writer = new ArrayBufferWriter<byte>();
        UrlEncodedSerializer.Deserialize<C>(json.AsSpan(), writer);
        var bytes = writer.WrittenSpan.ToArray();
        var text = Encoding.UTF8.GetString(bytes);
        Assert.That(text, Is.EqualTo(_query));

        // 3) Deserialize(ReadOnlySpan<char>, Type, writer)
        var writer2 = new ArrayBufferWriter<byte>();
        UrlEncodedSerializer.Deserialize(json.AsSpan(), typeof(C), writer2);
        var text2 = Encoding.UTF8.GetString(writer2.WrittenSpan.ToArray());
        Assert.That(text2, Is.EqualTo(_query));
    }

    [Test]
    public void TestJsonElementDeserializeOverloads()
    {
        // Build JsonElement from JSON string
        var json = UrlEncodedSerializer.Serialize<C>(_query.AsSpan()) ?? "{}";
        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // 1) Deserialize<T>(JsonElement)
        var q1 = UrlEncodedSerializer.Deserialize<C>(element);
        Assert.That(q1, Is.EqualTo(_query));

        // 2) Deserialize(JsonElement, Type)
        var q2 = UrlEncodedSerializer.Deserialize(element, typeof(C));
        Assert.That(q2, Is.EqualTo(_query));

        // 3) Deserialize<T>(JsonElement, writer)
        var writer = new ArrayBufferWriter<byte>();
        UrlEncodedSerializer.Deserialize<C>(element, writer);
        var text = Encoding.UTF8.GetString(writer.WrittenSpan.ToArray());
        Assert.That(text, Is.EqualTo(_query));

        // 4) Deserialize(JsonElement, Type, writer)
        var writer2 = new ArrayBufferWriter<byte>();
        UrlEncodedSerializer.Deserialize(element, typeof(C), writer2);
        var text2 = Encoding.UTF8.GetString(writer2.WrittenSpan.ToArray());
        Assert.That(text2, Is.EqualTo(_query));
    }

    [Test]
    public void TestSerializeToWriterAndNode()
    {
        // 1) Serialize to Utf8JsonWriter using JsonTypeInfo
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.MakeReadOnly(true); // ensure the TypeInfoResolver is populated
        var typeInfo = options.GetTypeInfo(typeof(C));
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            UrlEncodedSerializer.Serialize(_query.AsSpan(), typeInfo, writer, options);
        }

        var jsonFromWriter = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
        var jsonFromHelper = UrlEncodedSerializer.Serialize(_query.AsSpan(), typeInfo, options);
        Assert.That(jsonFromWriter, Is.EqualTo(jsonFromHelper));

        // 2) Serialize to Utf8JsonWriter using Type overload
        var buffer2 = new ArrayBufferWriter<byte>();
        using (var writer2 = new Utf8JsonWriter(buffer2))
        {
            UrlEncodedSerializer.Serialize(_query.AsSpan(), typeof(C), writer2, options);
        }

        var jsonFromWriter2 = Encoding.UTF8.GetString(buffer2.WrittenSpan.ToArray());
        Assert.That(jsonFromWriter2, Is.EqualTo(jsonFromHelper));

        // 3) Serialize<T>(query, writer)
        var buffer3 = new ArrayBufferWriter<byte>();
        using (var writer3 = new Utf8JsonWriter(buffer3))
        {
            UrlEncodedSerializer.Serialize<C>(_query.AsSpan(), writer3, options);
        }

        var jsonFromWriter3 = Encoding.UTF8.GetString(buffer3.WrittenSpan.ToArray());
        Assert.That(jsonFromWriter3, Is.EqualTo(jsonFromHelper));

        // 4) SerializeToNode<T>
        var node = UrlEncodedSerializer.SerializeToNode<C>(_query.AsSpan(), options);
        Assert.That(node, Is.Not.Null);
        Assert.That(node?.ToJsonString(), Is.EqualTo(jsonFromHelper));
    }

    [Test]
    public void TestSerializeAndDeserializeWithDefaultOptions()
    {
        // Ensure default options path works
        var json = UrlEncodedSerializer.Serialize<C>(_query.AsSpan());
        Assert.That(json, Is.Not.Null);

        var roundTripQuery = UrlEncodedSerializer.Deserialize(json ?? "", typeof(C));
        Assert.That(roundTripQuery, Is.EqualTo(_query));
    }

    [Test]
    public void TestStronglyTypedPrimitive()
    {
        P p = new(1, new("Paul", "Steiner"), new(12), null!);
        var query = string.Join('&', [
            "Id=1",
            "Name.First=Paul",
            "Name.Last=Steiner",
            "Age=12",
        ]);
        var json = UrlEncodedSerializer.Serialize<P>(query);
        var obj = JsonSerializer.Deserialize<P>(json ?? "")!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(obj, Is.Not.Null);
            Assert.That(obj.Id, Is.EqualTo(p.Id));
            Assert.That(obj.Name, Is.EqualTo(p.Name));
            Assert.That(obj.Age, Is.EqualTo(new Age(12)));
        }
        var queryRoundTrip = UrlEncodedSerializer.Deserialize<P>(json);
        Assert.That(queryRoundTrip, Is.EqualTo(query));
    }

    private void AssertEqualsC(C? obj)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(obj?.Items.Count, Is.EqualTo(_c.Items.Count));
            Assert.That(obj?.Items[0].Id, Is.EqualTo(_c.Items[0].Id));
            Assert.That(obj?.Items[0].Name, Is.EqualTo(_c.Items[0].Name));
            Assert.That(obj?.Items[0].MyInterests, Is.Null);
            Assert.That(obj?.Metadata.Customer.Name, Is.EqualTo(_c.Metadata.Customer.Name));
            Assert.That(obj?.Metadata.Customer.MyInterests, Is.EquivalentTo(_c.Metadata.Customer.MyInterests));
            Assert.That(obj?.Metadata.CreatedTs, Is.EqualTo(_c.Metadata.CreatedTs));
            Assert.That(obj?.Matrix, Is.EquivalentTo(_c.Matrix));
        }
    }
}

internal sealed record C(IImmutableList<P> Items, M Metadata, List<List<double?>> Matrix);

internal sealed record P(long Id, N Name, Age Age, IImmutableSet<string> MyInterests);

internal sealed record N(string First, string Last);

internal sealed record M(P Customer, [property: JsonPropertyName("Created.Ts")] DateTimeOffset CreatedTs);
[JsonConverter(typeof(AgeConverter))]
internal sealed record Age(int Value);

internal sealed class AgeConverter : JsonConverter<Age?>
{
    public override Age? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<int?>(ref reader, options) is {} v ? new(v) : null;
    }

    public override void Write(Utf8JsonWriter writer, Age? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value?.Value, options);
    }
}
#pragma warning restore CA2263 // Prefer generic overload when type is known

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrlEncodedToJson.Tests;

[TestFixture]
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
public class JsonSourceContextTests
{
    [Test]
    public void GenericApi_WorksWithSourceGeneratedContextOptions()
    {
        const string query = "MyInterests=MTB&MyInterests=HEMA";
        var json = UrlEncodedSerializer.Serialize<SerializationTests.SimpleCollectionModel>(query, AdditionalCoverageContext.Default);
        var roundTrip = UrlEncodedSerializer.Deserialize<SerializationTests.SimpleCollectionModel>(json ?? string.Empty, AdditionalCoverageContext.Default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(json, Is.EqualTo("{\"MyInterests\":[\"MTB\",\"HEMA\"]}"));
            Assert.That(roundTrip, Is.EqualTo(query));
        }
    }

    [Test]
    public void Serialize_JsonTypeInfoOverload_WritesExpectedJson_WithSourceGeneratedMetadata()
    {
        const string query = "Value=12";
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            UrlEncodedSerializer.Serialize<SerializationTests.StringBackedNumberContainer>(query, writer, AdditionalCoverageContext.Default);
        }

        var json = Encoding.UTF8.GetString(buffer.WrittenSpan);

        using var document = JsonDocument.Parse(json);
        var valueProperty = document.RootElement.GetProperty("Value");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(valueProperty.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(valueProperty.GetString(), Is.EqualTo("12"));
        }
    }
}


[JsonSourceGenerationOptions]
[JsonSerializable(typeof(SerializationTests.SimpleCollectionModel))]
[JsonSerializable(typeof(SerializationTests.StringBackedNumberContainer))]
internal partial class AdditionalCoverageContext : JsonSerializerContext;

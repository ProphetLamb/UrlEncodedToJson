using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UrlEncodedToJson.Tests;

[TestFixture]
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
public class SerializationTests
{
    [Test]
    public void Serialize_RepeatedSimpleValues_AppendsCollectionItems()
    {
        const string query = "MyInterests=MTB&MyInterests=HEMA&MyInterests=Golf";

        var json = UrlEncodedSerializer.Serialize<SimpleCollectionModel>(query);
        var model = JsonSerializer.Deserialize<SimpleCollectionModel>(json ?? string.Empty);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.MyInterests, Is.EqualTo(new[] { "MTB", "HEMA", "Golf" }));
            Assert.That(UrlEncodedSerializer.Deserialize<SimpleCollectionModel>(json), Is.EqualTo(query));
        }
    }

    [Test]
    public void Serialize_DuplicateIndexedComplexValue_UsesLatestAssignment()
    {
        const string query = "Items.0.Id=12&Items.0.Id=11&Items.0.Name=Updated";

        var json = UrlEncodedSerializer.Serialize<IndexedItemsModel>(query);
        var model = JsonSerializer.Deserialize<IndexedItemsModel>(json ?? string.Empty);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Items, Has.Count.EqualTo(1));
            Assert.That(model.Items[0].Id, Is.EqualTo(11));
            Assert.That(model.Items[0].Name, Is.EqualTo("Updated"));
        }
    }

    [Test]
    public void Serialize_SparseIndexedArray_FillsIntermediateEntriesWithNull()
    {
        const string query = "Values.2=7";

        var json = UrlEncodedSerializer.Serialize<SparseArrayModel>(query);
        var model = JsonSerializer.Deserialize<SparseArrayModel>(json ?? string.Empty);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Values, Is.EqualTo(new int?[] { null, null, 7 }));
            Assert.That(UrlEncodedSerializer.Deserialize<SparseArrayModel>(json), Is.EqualTo(query));
        }
    }

    [Test]
    public void Serialize_PrimitiveTypes_UseTypedJsonTokens()
    {
        const string query = "Count=42&Enabled=true&OptionalCount=null&Name=Ren%C3%A9";
        var json = UrlEncodedSerializer.Serialize<PrimitiveTokenModel>(query) ?? string.Empty;

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.GetProperty("Count").ValueKind, Is.EqualTo(JsonValueKind.Number));
            Assert.That(root.GetProperty("Count").GetInt32(), Is.EqualTo(42));
            Assert.That(root.GetProperty("Enabled").ValueKind, Is.EqualTo(JsonValueKind.True));
            Assert.That(root.GetProperty("OptionalCount").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(root.GetProperty("Name").ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(root.GetProperty("Name").GetString(), Is.EqualTo("René"));
        }
    }

    [Test]
    public void Serialize_PlusEscapes_AreDecodedToSpaces()
    {
        const string query = "FullName=Ren%C3%A9+Carannante";

        var node = UrlEncodedSerializer.SerializeToNode<SpaceModel>(query);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(node, Is.Not.Null);
            Assert.That(node!["FullName"]!.GetValue<string>(), Is.EqualTo("René Carannante"));
        }
    }

    [Test]
    public void Serialize_CustomStringConverter_PreservesNumericLookingTextAsString()
    {
        const string query = "Value=12";

        var json = UrlEncodedSerializer.Serialize<StringBackedNumberContainer>(query) ?? string.Empty;
        using var document = JsonDocument.Parse(json);
        var valueProperty = document.RootElement.GetProperty("Value");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(valueProperty.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(valueProperty.GetString(), Is.EqualTo("12"));
            Assert.That(UrlEncodedSerializer.Deserialize<StringBackedNumberContainer>(json), Is.EqualTo(query));
        }
    }

    [Test]
    public void SerializeAndDeserialize_WithCustomNamingPolicy_EscapesDotsAfterPolicy()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new DotNamingPolicy(),
        };

        const string query = "created%2Ets=2026-06-09T16%3A41%3A12Z";
        var json = UrlEncodedSerializer.Serialize<PolicyModel>(query, options) ?? string.Empty;

        using var document = JsonDocument.Parse(json);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(document.RootElement.TryGetProperty("created.ts", out var createdTs), Is.True);
            Assert.That(createdTs.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(createdTs.GetString(), Is.EqualTo("2026-06-09T16:41:12Z"));
            Assert.That(UrlEncodedSerializer.Deserialize<PolicyModel>(json, options), Is.EqualTo(query));
        }
    }

    [Test]
    public void SerializeToNode_TypeOverload_MatchesGenericOverload()
    {
        const string query = "MyInterests=MTB&MyInterests=HEMA";

        JsonNode? generic = UrlEncodedSerializer.SerializeToNode<SimpleCollectionModel>(query);
        JsonNode? nonGeneric = UrlEncodedSerializer.SerializeToNode(query, typeof(SimpleCollectionModel));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generic, Is.Not.Null);
            Assert.That(nonGeneric, Is.Not.Null);
            Assert.That(nonGeneric!.ToJsonString(), Is.EqualTo(generic!.ToJsonString()));
        }
    }

    [Test]
    public void Deserialize_SimpleArrayJson_UsesAppendSyntax()
    {
        const string json = "{\"MyInterests\":[\"MTB\",\"HEMA\",\"Golf\"]}";

        var query = UrlEncodedSerializer.Deserialize<SimpleCollectionModel>(json);

        Assert.That(query, Is.EqualTo("MyInterests=MTB&MyInterests=HEMA&MyInterests=Golf"));
    }

    internal sealed record SimpleCollectionModel(List<string> MyInterests);

    internal sealed record IndexedItemsModel(List<IndexedItem> Items);

    internal sealed record IndexedItem(int Id, string Name);

    internal sealed record SparseArrayModel(List<int?> Values);

    internal sealed record PrimitiveTokenModel(int Count, bool Enabled, int? OptionalCount, string Name);

    internal sealed record SpaceModel(string FullName);

    internal sealed record PolicyModel(string CreatedTs);

    internal sealed record StringBackedNumberContainer(StringBackedNumber Value);

    [JsonConverter(typeof(StringBackedNumberConverter))]
    internal sealed record StringBackedNumber(string Value);

    internal sealed class StringBackedNumberConverter : JsonConverter<StringBackedNumber?>
    {
        public override StringBackedNumber? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.String => new(reader.GetString()!),
                _ => throw new JsonException($"Expected string token but got {reader.TokenType}.")
            };
        }

        public override void Write(Utf8JsonWriter writer, StringBackedNumber? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value.Value);
            }
        }
    }

    internal sealed class DotNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            return name == nameof(PolicyModel.CreatedTs) ? "created.ts" : name;
        }
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolicySmoke;

internal sealed class AuthorJsonConverter : JsonConverter<Author>
{
    public override Author Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, Author value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Id", value.Id);
        writer.WriteString("Name", value.Name);
        writer.WriteStartArray("Books");
        foreach (var book in value.Books) writer.WriteNumberValue(book.Id);
        writer.WriteEndArray();
        writer.WriteStartArray("Tags");
        foreach (var tag in value.Tags) writer.WriteNumberValue(tag.Id);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

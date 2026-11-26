using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PolicySmoke;

internal static class SmokeJsonContext
{
    internal static JsonTypeInfo<Author> Author { get; } =
        JsonMetadataServices.CreateValueInfo<Author>(new JsonSerializerOptions { TypeInfoResolver = JsonTypeInfoResolver.Combine() }, new AuthorJsonConverter());
}

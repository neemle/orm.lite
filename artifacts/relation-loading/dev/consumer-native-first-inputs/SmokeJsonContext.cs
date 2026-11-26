using System.Text.Json.Serialization;

namespace PolicySmoke;

[JsonSerializable(typeof(Author))]
internal partial class SmokeJsonContext : JsonSerializerContext { }

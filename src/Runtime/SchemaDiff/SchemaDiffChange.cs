namespace Neemle.OrmLite;

public sealed record SchemaDiffChange(
    SchemaDiffChangeKind Kind, SchemaObjectRef Object, SchemaDiffImpact Impact, string Category);

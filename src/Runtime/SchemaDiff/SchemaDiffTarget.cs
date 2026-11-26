namespace Neemle.OrmLite;

public sealed record SchemaDiffTarget(
    OrmSchema Schema, string ModelVersion, SchemaDiffScope Scope, SchemaDiffLimits Limits);

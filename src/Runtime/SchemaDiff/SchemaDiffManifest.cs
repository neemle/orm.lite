namespace Neemle.OrmLite;

public sealed record SchemaDiffManifest(
    SchemaDiffIdentity Identity, SchemaDiffScope Scope, SchemaDiffProvenance Provenance, string SchemaToken);

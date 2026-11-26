namespace Neemle.OrmLite;

public sealed record SchemaDiffRecipe(
    SchemaDiffManifest Manifest, SchemaDiffUnitNodes Nodes, SchemaDiffObservation Before, SchemaDiffObservation After);

namespace Neemle.OrmLite;

internal static class SchemaSourceRecipe
{
    internal static string Write(SchemaDiffRecipe recipe)
    {
        var w = new SchemaSourceBuilder();
        var manifest = Manifest(w, recipe.Manifest);
        var nodes = w.Call("R.SchemaDiffUnitNodes", "new R.SchemaDiffUnitNodes",
            w.List("N.Node", recipe.Nodes.Up.Select(n => SchemaSourceNodes.Write(w, n))),
            w.List("N.Node", recipe.Nodes.Down.Select(n => SchemaSourceNodes.Write(w, n))));
        var value = w.Call("R.SchemaDiffRecipe", "new R.SchemaDiffRecipe", manifest, nodes,
            SchemaSourceFacts.Observation(w, recipe.Before), SchemaSourceFacts.Observation(w, recipe.After));
        return w.Finish(recipe.Manifest.Identity, value);
    }

    private static string Manifest(SchemaSourceBuilder w, SchemaDiffManifest manifest)
    {
        var id = manifest.Identity; var provenance = manifest.Provenance;
        return w.Call("R.SchemaDiffManifest", "new R.SchemaDiffManifest",
            w.Call("R.SchemaDiffIdentity", "new R.SchemaDiffIdentity", w.Text(id.VersionPrefix),
                w.Text(id.CSharpNamespace), w.Text(id.CSharpTypeName)), SchemaSourceTarget.Scope(w, manifest.Scope),
            w.Call("R.SchemaDiffProvenance", "new R.SchemaDiffProvenance", w.Text(provenance.ModelVersion),
                w.Text(provenance.TargetDigest), w.Text(provenance.ObservationDigest),
                w.Text(provenance.ProviderProfile)),
            w.Text(manifest.SchemaToken));
    }
}

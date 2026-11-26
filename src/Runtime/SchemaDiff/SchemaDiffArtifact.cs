namespace Neemle.OrmLite;

public sealed class SchemaDiffArtifact
{
    private SchemaDiffArtifact(SchemaDiffRecipe recipe)
    {
        Recipe = recipe;
        Version = SchemaRecipeIdentity.Version(recipe);
        CoverageVersion = Version + "__schema_diff_coverage_" + SchemaDiffCanonical.Hash(recipe.Manifest.SchemaToken);
        ReviewDigest = SchemaRecipeIdentity.Review(recipe);
        CSharpSource = SchemaSourceRecipe.Write(recipe);
    }

    public string Version { get; }
    public string CoverageVersion { get; }
    public string ReviewDigest { get; }
    public string CSharpSource { get; }
    public Reversibility Reversibility => Reversibility.Reversible;
    public bool DownProofRequired => true;
    internal SchemaDiffRecipe Recipe { get; }

    public static SchemaDiffArtifact FromRecipe(SchemaDiffRecipe recipe) => new(SchemaRecipeValidation.Copy(recipe));
}

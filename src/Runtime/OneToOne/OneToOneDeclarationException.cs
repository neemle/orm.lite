namespace Neemle.OrmLite;

public sealed class OneToOneDeclarationException : InvalidOperationException
{
    internal OneToOneDeclarationException(OneToOneError error, string? entity, string? navigation = null)
        : base("One-to-one declaration " + error + ". Entity: " + Safe(entity) + "; navigation: " + Safe(navigation))
    { Error = error; Entity = Safe(entity); Navigation = Safe(navigation); }

    public OneToOneError Error { get; }
    public string? Entity { get; }
    public string? Navigation { get; }
    private static string? Safe(string? value) => ForeignKeyValidation.ValidName(value) ? value : null;
}

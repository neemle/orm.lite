namespace Neemle.OrmLite;

public sealed class RelationScopeException : InvalidOperationException
{
    public RelationScopeException(RelationScopeError error, string? navigation = null)
        : base("Relation loading refused: " + error + (navigation is null ? "." : " (" + navigation + ")."))
    { Error = error; Navigation = navigation; }

    public RelationScopeError Error { get; }
    public string? Navigation { get; }
}

namespace Neemle.OrmLite;

public sealed class OneToOneCardinalityException : InvalidOperationException
{
    internal OneToOneCardinalityException(string entity, string navigation)
        : base("One-to-one cardinality is invalid for " + entity + "." + navigation + ".")
    { Entity = entity; Navigation = navigation; }

    public string Entity { get; }
    public string Navigation { get; }
}

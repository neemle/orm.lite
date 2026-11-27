namespace Neemle.OrmLite;

/// <summary>A REINDEX of the named scope in the named mode.</summary>
/// <param name="Scope">The scope; required.</param>
/// <param name="Mode">The mode; required.</param>
public sealed record ReindexRequest(ReindexScope Scope, ReindexMode Mode)
{
    /// <summary>The table, by its generated descriptor: required for Table and Index, null for Database.</summary>
    public EntityDescriptor? Table { get; init; }

    /// <summary>The index for <see cref="ReindexScope.Index"/>: one of <see cref="Table"/>'s <c>Indexes</c>; null otherwise.</summary>
    public IndexDescriptor? Index { get; init; }

    /// <summary>The whole budget of the call; null takes the provider's maintenance timeout.</summary>
    public TimeSpan? Timeout { get; init; }
}

namespace Neemle.OrmLite;

public partial interface IMaintenanceProvider
{
    /// <summary>
    /// Renders REINDEX of the scope in exactly <paramref name="mode"/>, naming <paramref name="table"/> or
    /// <paramref name="index"/> quoted by the provider. For <see cref="ReindexScope.Index"/>,
    /// <paramref name="indexSchema"/> is the schema <see cref="RenderIndexSchemaLookup"/> answered, or null when the
    /// provider renders no lookup.
    /// </summary>
    string RenderReindex(ReindexScope scope, ReindexMode mode, EntityDescriptor? table, IndexDescriptor? index, string? indexSchema) =>
        throw new NotSupportedException("this maintenance provider does not render REINDEX.");

    /// <summary>
    /// A query answering, in one value, the schema the indexes of <paramref name="table"/> live in, run on the
    /// maintenance connection before an <see cref="ReindexScope.Index"/> REINDEX; or null when an index name alone
    /// names exactly one index. It is session plumbing rendered from the descriptor only and is not reported to
    /// diagnostics. Null by default.
    /// </summary>
    string? RenderIndexSchemaLookup(EntityDescriptor table) => null;
}

namespace Neemle.OrmLite;

public partial interface IMaintenanceProvider
{
    /// <summary>
    /// Renders REINDEX of the scope in exactly <paramref name="mode"/>, naming <paramref name="table"/> or
    /// <paramref name="index"/> quoted by the provider.
    /// </summary>
    string RenderReindex(ReindexScope scope, ReindexMode mode, EntityDescriptor? table, IndexDescriptor? index) =>
        throw new NotSupportedException("this maintenance provider does not render REINDEX.");
}

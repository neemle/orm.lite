namespace Neemle.OrmLite.Tests.Maintenance;

internal sealed partial class TunedSqliteProvider
{
    public string RenderReindex(ReindexScope scope, ReindexMode mode, EntityDescriptor? table, IndexDescriptor? index) =>
        Inner.RenderReindex(scope, mode, table, index);
}

namespace Neemle.OrmLite.Tests.Maintenance;

internal sealed partial class TunedSqliteProvider
{
    public string RenderAnalyze(AnalyzeScope scope, EntityDescriptor? table) => Inner.RenderAnalyze(scope, table);
}

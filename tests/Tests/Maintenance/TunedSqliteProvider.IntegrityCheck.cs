using System.Data.Common;

namespace Neemle.OrmLite.Tests.Maintenance;

internal sealed partial class TunedSqliteProvider
{
    public string RenderIntegrityCheck(IntegrityCheckMode mode, EntityDescriptor? table, int maxFindings) =>
        Inner.RenderIntegrityCheck(mode, table, maxFindings);

    public IntegrityFinding? ReadIntegrityFinding(IntegrityCheckMode mode, DbDataReader row) => Inner.ReadIntegrityFinding(mode, row);

    public int? IntegrityEngineCap(IntegrityCheckMode mode, EntityDescriptor? table) => Inner.IntegrityEngineCap(mode, table);
}

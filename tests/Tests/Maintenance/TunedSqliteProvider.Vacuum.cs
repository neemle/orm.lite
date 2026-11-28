using System.Data.Common;

namespace Neemle.OrmLite.Tests.Maintenance;

internal sealed partial class TunedSqliteProvider
{
    // A session guard to run around every maintenance statement, and a statement text that replaces SQLite's VACUUM.
    internal MaintenanceSessionGuard? Guard { get; init; }

    internal string? VacuumText { get; init; }

    public bool HasWriterGate => Inner.HasWriterGate;

    public bool IsContention(DbException failure) => Inner.IsContention(failure);

    public MaintenanceSessionGuard? RenderLockGuard(TimeSpan remaining) => Guard;

    public string RenderVacuum(VacuumMode mode, EntityDescriptor? table) => VacuumText ?? Inner.RenderVacuum(mode, table);
}

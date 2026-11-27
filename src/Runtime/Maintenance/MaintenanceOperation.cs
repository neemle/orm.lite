namespace Neemle.OrmLite;

/// <summary>The maintenance operation a <see cref="IMaintenanceResult"/> reports on. Members are only ever appended.</summary>
public enum MaintenanceOperation
{
    /// <summary>A WAL checkpoint (<see cref="OrmMaintenance.CheckpointWalAsync"/>).</summary>
    WalCheckpoint = 1,

    /// <summary>A VACUUM (<see cref="OrmMaintenance.VacuumAsync"/>).</summary>
    Vacuum,

    /// <summary>An ANALYZE (<see cref="OrmMaintenance.AnalyzeAsync"/>).</summary>
    Analyze,

    /// <summary>A REINDEX (<see cref="OrmMaintenance.ReindexAsync"/>).</summary>
    Reindex,
}

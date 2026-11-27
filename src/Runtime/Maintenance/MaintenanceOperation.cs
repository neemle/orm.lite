namespace Neemle.OrmLite;

/// <summary>The maintenance operation a <see cref="IMaintenanceResult"/> reports on. Members are only ever appended.</summary>
public enum MaintenanceOperation
{
    /// <summary>A WAL checkpoint (<see cref="OrmMaintenance.CheckpointWalAsync"/>).</summary>
    WalCheckpoint = 1,
}

namespace Neemle.OrmLite;

/// <summary>
/// The outcome of a WAL checkpoint. <see cref="WalFrames"/> and <see cref="CheckpointedFrames"/> are SQLite's log and
/// checkpointed counts, verbatim; both are null when no row was read (the writer gate was busy) or the database is not
/// in WAL mode.
/// </summary>
public sealed record WalCheckpointResult(
    string Provider,
    WalCheckpointMode Mode,
    MaintenanceStatus Status,
    long? WalFrames,
    long? CheckpointedFrames) : IMaintenanceResult
{
    /// <inheritdoc />
    public MaintenanceOperation Operation => MaintenanceOperation.WalCheckpoint;

    /// <inheritdoc />
    public bool IsComplete => Status == MaintenanceStatus.Completed;
}

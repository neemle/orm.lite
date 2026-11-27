namespace Neemle.OrmLite;

/// <summary>
/// The outcome of a VACUUM. <see cref="Checkpoint"/> is the WAL checkpoint run before it on a provider that checkpoints
/// first (SQLite); null when none ran, or when the writer gate was busy before the checkpoint could run.
/// </summary>
public sealed record VacuumResult(
    string Provider,
    VacuumMode Mode,
    string? Table,
    MaintenanceStatus Status,
    WalCheckpointResult? Checkpoint) : IMaintenanceResult
{
    /// <inheritdoc />
    public MaintenanceOperation Operation => MaintenanceOperation.Vacuum;

    /// <inheritdoc />
    public bool IsComplete => Status == MaintenanceStatus.Completed;
}

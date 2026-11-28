namespace Neemle.OrmLite;

/// <summary>The outcome of a REINDEX: <see cref="MaintenanceStatus.Completed"/> or <see cref="MaintenanceStatus.Busy"/>.</summary>
public sealed record ReindexResult(
    string Provider,
    ReindexScope Scope,
    ReindexMode Mode,
    string? Table,
    string? Index,
    MaintenanceStatus Status) : IMaintenanceResult
{
    /// <inheritdoc />
    public MaintenanceOperation Operation => MaintenanceOperation.Reindex;

    /// <inheritdoc />
    public bool IsComplete => Status == MaintenanceStatus.Completed;
}

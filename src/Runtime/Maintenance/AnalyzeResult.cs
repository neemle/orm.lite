namespace Neemle.OrmLite;

/// <summary>The outcome of an ANALYZE: <see cref="MaintenanceStatus.Completed"/> or <see cref="MaintenanceStatus.Busy"/>.</summary>
public sealed record AnalyzeResult(
    string Provider,
    AnalyzeScope Scope,
    string? Table,
    MaintenanceStatus Status) : IMaintenanceResult
{
    /// <inheritdoc />
    public MaintenanceOperation Operation => MaintenanceOperation.Analyze;

    /// <inheritdoc />
    public bool IsComplete => Status == MaintenanceStatus.Completed;
}

namespace Neemle.OrmLite;

/// <summary>
/// The outcome of an integrity check: <see cref="MaintenanceStatus.Completed"/> with no findings,
/// <see cref="MaintenanceStatus.IssuesFound"/> with findings, or <see cref="MaintenanceStatus.Busy"/>.
/// <see cref="Truncated"/> is true when the check reported more than the request's <c>MaxFindings</c>.
/// </summary>
public sealed record IntegrityCheckResult(
    string Provider,
    IntegrityCheckMode Mode,
    MaintenanceStatus Status,
    IReadOnlyList<IntegrityFinding> Findings,
    bool Truncated) : IMaintenanceResult
{
    /// <inheritdoc />
    public MaintenanceOperation Operation => MaintenanceOperation.IntegrityCheck;

    /// <inheritdoc />
    public bool IsComplete => Status == MaintenanceStatus.Completed;
}

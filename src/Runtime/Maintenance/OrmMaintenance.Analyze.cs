namespace Neemle.OrmLite;

public sealed partial class OrmMaintenance
{
    /// <summary>
    /// Refreshes the query planner's statistics for the whole database or one table. It never changes data. On a
    /// provider with a writer gate (SQLite, whose ANALYZE writes <c>sqlite_stat1</c>) it takes the gate and refuses a
    /// flow whose open UnitOfWork holds it; PostgreSQL takes no gate, and its lock wait is bounded by the budget.
    /// Contention is <see cref="MaintenanceStatus.Busy"/>; any other failure (a missing table) propagates unchanged.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeAsync(AnalyzeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMode(request.Scope, nameof(request.Scope));
        if (request.Scope == AnalyzeScope.Table && request.Table is null)
            throw new ArgumentException("AnalyzeScope.Table needs the table's descriptor in Table.", nameof(request));
        if (request.Scope == AnalyzeScope.Database && request.Table is not null)
            throw new ArgumentException("AnalyzeScope.Database analyzes every table; leave Table null or use AnalyzeScope.Table.", nameof(request));
        RefuseView(request.Table, nameof(AnalyzeAsync));
        ValidateTimeout(request.Timeout);
        ct.ThrowIfCancellationRequested();
        var port = Require(Capability.Analyze, nameof(AnalyzeAsync));
        return RunAsync(new MaintenanceStep<AnalyzeResult>(
            MaintenanceOperation.Analyze, port, port.RenderAnalyze(request.Scope, request.Table), port.HasWriterGate,
            async (reader, token) =>
            {
                while (await reader.ReadAsync(token)) { }
                return Analyzed(request, MaintenanceStatus.Completed);
            },
            () => Analyzed(request, MaintenanceStatus.Busy)), Budget(port, request.Timeout), ct);
    }

    private AnalyzeResult Analyzed(AnalyzeRequest request, MaintenanceStatus status) =>
        new(ProviderId, request.Scope, request.Table?.Table, status);
}

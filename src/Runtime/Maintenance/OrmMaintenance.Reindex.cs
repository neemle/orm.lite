namespace Neemle.OrmLite;

public sealed partial class OrmMaintenance
{
    /// <summary>
    /// Rebuilds the indexes of the database, of one table or one declared index, in exactly the requested mode. It never
    /// changes table data and never drops an index. <see cref="ReindexMode.Concurrent"/> on a provider without it is
    /// refused with <see cref="CapabilityNotSupportedException"/> (<see cref="Capability.ReindexConcurrently"/>), never
    /// downgraded. On a provider with a writer gate (SQLite) it takes the gate and refuses a flow whose open UnitOfWork
    /// holds it. Contention is <see cref="MaintenanceStatus.Busy"/>; any other failure propagates unchanged.
    /// </summary>
    public Task<ReindexResult> ReindexAsync(ReindexRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMode(request.Scope, nameof(request.Scope));
        ValidateMode(request.Mode, nameof(request.Mode));
        ValidateReindexTarget(request);
        RefuseView(request.Table, nameof(ReindexAsync));
        ValidateTimeout(request.Timeout);
        ct.ThrowIfCancellationRequested();
        var port = Require(Capability.Reindex, nameof(ReindexAsync));
        if (request.Mode == ReindexMode.Concurrent) RequireOption(Capability.ReindexConcurrently, nameof(ReindexAsync));
        // ADR D12.5: an index is named through its table's schema where the provider asks for it, never through
        // search_path alone.
        var lookup = request.Scope == ReindexScope.Index && port.RenderIndexSchemaLookup(request.Table!) is { } query
            ? new MaintenanceLookup(query, schema => port.RenderReindex(request.Scope, request.Mode, request.Table, request.Index, schema))
            : null;
        return Execution(port, request.Timeout, ct).RunAsync(new MaintenanceStep<ReindexResult>(
            MaintenanceOperation.Reindex, port, port.HasWriterGate,
            lookup is null
                ? () => port.RenderReindex(request.Scope, request.Mode, request.Table, request.Index, null)
                : null,
            () => new DrainedRows<ReindexResult>(() => Reindexed(request, MaintenanceStatus.Completed)),
            () => Reindexed(request, MaintenanceStatus.Busy),
            lookup));
    }

    private static void ValidateReindexTarget(ReindexRequest request)
    {
        if (request.Scope == ReindexScope.Database && (request.Table is not null || request.Index is not null))
            throw new ArgumentException("ReindexScope.Database rebuilds every index; leave Table and Index null.", nameof(request));
        if (request.Scope == ReindexScope.Table && (request.Table is null || request.Index is not null))
            throw new ArgumentException("ReindexScope.Table needs the table's descriptor in Table and no Index.", nameof(request));
        if (request.Scope != ReindexScope.Index) return;
        if (request.Table is null || request.Index is null)
            throw new ArgumentException("ReindexScope.Index needs both the table's descriptor and one of its indexes.", nameof(request));
        if (!request.Table.Indexes.Contains(request.Index))
            throw new ArgumentException(
                "the index '" + request.Index.Name + "' is not one of the indexes declared on '" + request.Table.Table + "'.", nameof(request));
    }

    private ReindexResult Reindexed(ReindexRequest request, MaintenanceStatus status) =>
        new(ProviderId, request.Scope, request.Mode, request.Table?.Table, request.Index?.Name, status);
}

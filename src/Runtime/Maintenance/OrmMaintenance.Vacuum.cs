namespace Neemle.OrmLite;

public sealed partial class OrmMaintenance
{
    /// <summary>
    /// Runs VACUUM in exactly the requested mode. On a provider that checkpoints (SQLite) it first runs a
    /// <see cref="WalCheckpointMode.Truncate"/> checkpoint and issues VACUUM only when that completed or the database is
    /// not in WAL mode; otherwise the result is <see cref="MaintenanceStatus.NotRun"/> and carries the checkpoint. The
    /// writer gate is taken for each step and released between them; the request's timeout is one budget for both.
    /// A view descriptor is refused with <see cref="ArgumentException"/> during validation. FULL, or a table, on a
    /// provider without that option is refused with <see cref="CapabilityNotSupportedException"/>
    /// before any connection opens; so is a call from a flow whose open UnitOfWork is on the same database
    /// (<see cref="InvalidOperationException"/>).
    /// </summary>
    public Task<VacuumResult> VacuumAsync(VacuumRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMode(request.Mode, nameof(request.Mode));
        RefuseView(request.Table, nameof(VacuumAsync));
        ValidateTimeout(request.Timeout);
        ct.ThrowIfCancellationRequested();
        var port = Require(Capability.Vacuum, nameof(VacuumAsync));
        if (request.Mode == VacuumMode.Full) RequireOption(Capability.VacuumFull, nameof(VacuumAsync));
        if (request.Table is not null) RequireOption(Capability.VacuumTable, nameof(VacuumAsync));
        // A provider that checkpoints runs a Truncate checkpoint first, in the same execution (ADR D14.3).
        var checkpoint = _context.Provider.Capabilities.Of(Capability.WalCheckpoint) != Support.None
            ? CheckpointStep(port, WalCheckpointMode.Truncate)
            : null;
        return Execution(port, request.Timeout, ct).RunVacuumAsync(
            checkpoint,
            prior => new MaintenanceStep<VacuumResult>(
                MaintenanceOperation.Vacuum, port, true,
                () => port.RenderVacuum(request.Mode, request.Table),
                () => new DrainedRows<VacuumResult>(() => Vacuumed(request, MaintenanceStatus.Completed, prior)),
                () => Vacuumed(request, MaintenanceStatus.Busy, prior)),
            (status, prior) => Vacuumed(request, status, prior));
    }

    // ADR D5 (v2): a database with no WAL has nothing to checkpoint, so NotWal meets the prerequisite as Completed does.
    // Partial and Busy (a reader still needs frames) do not. Deliberately not IsComplete.
    internal static bool CheckpointLetsVacuumRun(WalCheckpointResult checkpoint) =>
        checkpoint.Status is MaintenanceStatus.Completed or MaintenanceStatus.NotWal;

    private VacuumResult Vacuumed(VacuumRequest request, MaintenanceStatus status, WalCheckpointResult? checkpoint) =>
        new(ProviderId, request.Mode, request.Table?.Table, status, checkpoint);
}

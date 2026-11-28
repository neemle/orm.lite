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
        return VacuumCoreAsync(port, request, Budget(port, request.Timeout), ct);
    }

    private async Task<VacuumResult> VacuumCoreAsync(
        IMaintenanceProvider port, VacuumRequest request, MaintenanceBudget budget, CancellationToken ct)
    {
        WalCheckpointResult? checkpoint = null;
        if (_context.Provider.Capabilities.Of(Capability.WalCheckpoint) != Support.None)
        {
            RefuseHolder(port, MaintenanceOperation.Vacuum);
            checkpoint = await CheckpointAsync(port, WalCheckpointMode.Truncate, budget, ct);
            if (checkpoint is { Status: MaintenanceStatus.Busy, WalFrames: null })
                return Vacuumed(request, MaintenanceStatus.Busy, null);
            if (!CheckpointLetsVacuumRun(checkpoint)) return Vacuumed(request, MaintenanceStatus.NotRun, checkpoint);
        }
        return await RunAsync(new MaintenanceStep<VacuumResult>(
            MaintenanceOperation.Vacuum, port, port.RenderVacuum(request.Mode, request.Table), true,
            async (reader, token) =>
            {
                while (await reader.ReadAsync(token)) { }
                return Vacuumed(request, MaintenanceStatus.Completed, checkpoint);
            },
            () => Vacuumed(request, MaintenanceStatus.Busy, checkpoint)), budget, ct);
    }

    // ADR D5 (v2): a database with no WAL has nothing to checkpoint, so NotWal meets the prerequisite as Completed does.
    // Partial and Busy (a reader still needs frames) do not. Deliberately not IsComplete.
    internal static bool CheckpointLetsVacuumRun(WalCheckpointResult checkpoint) =>
        checkpoint.Status is MaintenanceStatus.Completed or MaintenanceStatus.NotWal;

    private VacuumResult Vacuumed(VacuumRequest request, MaintenanceStatus status, WalCheckpointResult? checkpoint) =>
        new(ProviderId, request.Mode, request.Table?.Table, status, checkpoint);
}

using System.Data.Common;

namespace Neemle.OrmLite;

public sealed partial class OrmMaintenance
{
    /// <summary>
    /// Checkpoints the WAL of the context's database in exactly the requested mode. <see cref="WalCheckpointMode.Passive"/>
    /// never waits; the other modes first enter the database's writer gate. Contention is reported as
    /// <see cref="MaintenanceStatus.Busy"/>, a database that is not in WAL mode as <see cref="MaintenanceStatus.NotWal"/>,
    /// and any other native failure propagates unchanged. Refused with <see cref="CapabilityNotSupportedException"/> by a
    /// provider without <see cref="Capability.WalCheckpoint"/>, before any connection opens.
    /// </summary>
    public Task<WalCheckpointResult> CheckpointWalAsync(WalCheckpointRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMode(request.Mode, nameof(request.Mode));
        ValidateTimeout(request.Timeout);
        ct.ThrowIfCancellationRequested();
        var port = Require(Capability.WalCheckpoint, nameof(CheckpointWalAsync));
        return CheckpointAsync(port, request.Mode, Budget(port, request.Timeout), ct);
    }

    private Task<WalCheckpointResult> CheckpointAsync(
        IMaintenanceProvider port, WalCheckpointMode mode, MaintenanceBudget budget, CancellationToken ct) =>
        RunAsync(new MaintenanceStep<WalCheckpointResult>(
            MaintenanceOperation.WalCheckpoint, port, port.RenderWalCheckpoint(mode), mode != WalCheckpointMode.Passive,
            (reader, token) => ReadCheckpointAsync(reader, mode, token),
            () => new WalCheckpointResult(ProviderId, mode, MaintenanceStatus.Busy, null, null)), budget, ct);

    // SQLite answers with one row (busy, log, checkpointed); log = -1 means the database is not in WAL mode.
    private async Task<WalCheckpointResult> ReadCheckpointAsync(DbDataReader reader, WalCheckpointMode mode, CancellationToken ct)
    {
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("the provider's WAL checkpoint returned no row; it must return busy, log and checkpointed.");
        var busy = reader.GetInt64(0);
        var log = reader.GetInt64(1);
        var checkpointed = reader.GetInt64(2);
        if (log == -1) return new WalCheckpointResult(ProviderId, mode, MaintenanceStatus.NotWal, null, null);
        var status = busy != 0 ? MaintenanceStatus.Busy
            : checkpointed == log ? MaintenanceStatus.Completed
            : MaintenanceStatus.Partial;
        return new WalCheckpointResult(ProviderId, mode, status, log, checkpointed);
    }
}

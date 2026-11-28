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
        return Execution(port, request.Timeout, ct).RunAsync(CheckpointStep(port, request.Mode));
    }

    private MaintenanceStep<WalCheckpointResult> CheckpointStep(IMaintenanceProvider port, WalCheckpointMode mode) =>
        new(MaintenanceOperation.WalCheckpoint, port, mode != WalCheckpointMode.Passive,
            () => port.RenderWalCheckpoint(mode),
            () => new CheckpointRow(mode, ProviderId),
            () => new WalCheckpointResult(ProviderId, mode, MaintenanceStatus.Busy, null, null));

    // SQLite answers with one row (busy, log, checkpointed); log = -1 means the database is not in WAL mode.
    private sealed class CheckpointRow(WalCheckpointMode mode, string provider) : IMaintenanceRows<WalCheckpointResult>
    {
        private WalCheckpointResult? _result;

        public bool Accept(DbDataReader row)
        {
            var busy = row.GetInt64(0);
            var log = row.GetInt64(1);
            var checkpointed = row.GetInt64(2);
            var status = log == -1 ? MaintenanceStatus.NotWal
                : busy != 0 ? MaintenanceStatus.Busy
                : checkpointed == log ? MaintenanceStatus.Completed
                : MaintenanceStatus.Partial;
            _result = status == MaintenanceStatus.NotWal
                ? new WalCheckpointResult(provider, mode, status, null, null)
                : new WalCheckpointResult(provider, mode, status, log, checkpointed);
            return false;
        }

        public WalCheckpointResult Complete() =>
            _result ?? throw new InvalidOperationException(
                "the provider's WAL checkpoint returned no row; it must return busy, log and checkpointed.");
    }
}

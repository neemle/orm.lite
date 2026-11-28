using System.Data.Common;

namespace Neemle.OrmLite;

public sealed partial class OrmMaintenance
{
    /// <summary>
    /// Checks the database, or one table, and reports what is wrong as data: <see cref="MaintenanceStatus.IssuesFound"/>
    /// with at most <c>MaxFindings</c> findings. A view descriptor is refused with <see cref="ArgumentException"/>
    /// during validation. It is read-only, takes no writer gate and is allowed inside an open
    /// UnitOfWork's flow, where it sees committed data only. Contention is <see cref="MaintenanceStatus.Busy"/>; a database
    /// it cannot read at all propagates the native exception. Refused with <see cref="CapabilityNotSupportedException"/>
    /// by a provider without <see cref="Capability.IntegrityCheck"/>, before any connection opens.
    /// </summary>
    public Task<IntegrityCheckResult> CheckIntegrityAsync(IntegrityCheckRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMode(request.Mode, nameof(request.Mode));
        RefuseView(request.Table, nameof(CheckIntegrityAsync));
        if (request.MaxFindings is < 1 or > IntegrityCheckRequest.MaxFindingsLimit)
            throw new ArgumentOutOfRangeException("MaxFindings", request.MaxFindings,
                "MaxFindings must be 1 to " + IntegrityCheckRequest.MaxFindingsLimit + ".");
        ValidateTimeout(request.Timeout);
        ct.ThrowIfCancellationRequested();
        var port = Require(Capability.IntegrityCheck, nameof(CheckIntegrityAsync));
        return Execution(port, request.Timeout, ct).RunAsync(new MaintenanceStep<IntegrityCheckResult>(
            MaintenanceOperation.IntegrityCheck, port, false,
            () => port.RenderIntegrityCheck(request.Mode, request.Table, request.MaxFindings),
            () => new FindingRows(port, request, ProviderId),
            () => new IntegrityCheckResult(ProviderId, request.Mode, MaintenanceStatus.Busy, [], false)));
    }

    // Takes at most MaxFindings + 1 reporting rows: the extra one only tells that there were more. A form the engine
    // caps (ADR D12.6) is truncated too once the cap is reached: the engine may have stopped there.
    private sealed class FindingRows(IMaintenanceProvider port, IntegrityCheckRequest request, string provider)
        : IMaintenanceRows<IntegrityCheckResult>
    {
        private readonly List<IntegrityFinding> _findings = [];

        private int _seen;

        public bool Accept(DbDataReader row)
        {
            if (port.ReadIntegrityFinding(request.Mode, row) is not { } finding) return true;
            _seen++;
            if (_seen <= request.MaxFindings) _findings.Add(finding);
            return _seen <= request.MaxFindings;
        }

        public IntegrityCheckResult Complete()
        {
            var status = _findings.Count == 0 ? MaintenanceStatus.Completed : MaintenanceStatus.IssuesFound;
            var truncated = _seen > request.MaxFindings
                || port.IntegrityEngineCap(request.Mode, request.Table) is { } cap && _seen >= cap;
            return new IntegrityCheckResult(provider, request.Mode, status, _findings, truncated);
        }
    }
}

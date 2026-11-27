using System.Data.Common;

namespace Neemle.OrmLite;

/// <summary>
/// The optional provider port behind <see cref="OrmContext.Maintenance"/>. A provider that does not implement it has
/// every maintenance operation refused with <see cref="CapabilityNotSupportedException"/> before any connection opens.
/// The provider only renders statement text from enums and descriptors; the core runs it, reads the result and reports
/// it to diagnostics.
/// </summary>
public partial interface IMaintenanceProvider
{
    /// <summary>The budget a maintenance call gets when its request names no <c>Timeout</c>.</summary>
    TimeSpan MaintenanceTimeout { get; }

    /// <summary>
    /// The key of the database a data source writes to, the same one the provider's writer gate uses; null when the
    /// source names no database the provider can key.
    /// </summary>
    string? WriterKeyOf(DbDataSource source);

    /// <summary>
    /// Tries to enter the writer gate of the source's database within <paramref name="timeout"/>. A timeout is reported
    /// as <c>Entered = false</c>, never as an exception.
    /// </summary>
    ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(DbDataSource source, TimeSpan timeout, CancellationToken ct);

    /// <summary>
    /// True when the provider serialises writers in process through a writer gate, so operations that write must take
    /// it (and refuse a flow that already holds it). False by default.
    /// </summary>
    bool HasWriterGate => false;

    /// <summary>
    /// True when <paramref name="failure"/> is lock contention (the database was busy or a lock wait timed out), which
    /// a maintenance call reports as <see cref="MaintenanceStatus.Busy"/> instead of throwing. False by default.
    /// </summary>
    bool IsContention(DbException failure) => false;

    /// <summary>
    /// The session statements that bound a lock wait to <paramref name="remaining"/> on the maintenance connection, or
    /// null when the provider bounds it otherwise. They run on the same connection before and after the operation's
    /// statement and are not reported to diagnostics.
    /// </summary>
    MaintenanceSessionGuard? RenderLockGuard(TimeSpan remaining) => null;
}

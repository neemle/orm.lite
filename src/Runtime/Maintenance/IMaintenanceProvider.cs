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
}

using System.Data.Common;

namespace Neemle.OrmLite;

/// <summary>One statement of a maintenance operation, as the shared pipeline runs it.</summary>
internal sealed record MaintenanceStep<TResult>(
    MaintenanceOperation Operation,
    IMaintenanceProvider Port,
    string Text,
    bool TakesGate,
    Func<DbDataReader, CancellationToken, Task<TResult>> Read,
    Func<TResult> Busy);

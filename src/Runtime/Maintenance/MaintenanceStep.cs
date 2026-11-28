using System.Data.Common;

namespace Neemle.OrmLite;

/// <summary>
/// One statement of a maintenance operation, as the shared pipeline runs it: its text, or, when it needs a value only the
/// database knows (ADR D12.5), a lookup to run first and a render of the text from the lookup's answer.
/// </summary>
internal sealed record MaintenanceStep<TResult>(
    MaintenanceOperation Operation,
    IMaintenanceProvider Port,
    string? Text,
    bool TakesGate,
    Func<DbDataReader, CancellationToken, Task<TResult>> Read,
    Func<TResult> Busy,
    MaintenanceLookup? Lookup = null);

/// <summary>A non-diagnosed session query answering one value, and the statement text rendered from that value.</summary>
internal sealed record MaintenanceLookup(string Text, Func<string?, string> Render);

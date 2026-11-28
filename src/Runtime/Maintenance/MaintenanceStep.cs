namespace Neemle.OrmLite;

/// <summary>
/// One statement of a maintenance operation, as data for <see cref="MaintenanceExecution"/>: how to render its text
/// or, when it needs a value only the database knows (ADR D12.5), a lookup to run first and a render of the text from
/// the lookup's answer; the synchronous reducer of its rows; and the result it reports as Busy.
/// </summary>
internal sealed record MaintenanceStep<TResult>(
    MaintenanceOperation Operation,
    IMaintenanceProvider Port,
    bool TakesGate,
    Func<string>? Render,
    Func<IMaintenanceRows<TResult>> Rows,
    Func<TResult> Busy,
    MaintenanceLookup? Lookup = null) where TResult : IMaintenanceResult;

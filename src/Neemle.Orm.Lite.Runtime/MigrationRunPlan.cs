namespace Neemle.Orm.Lite;

// What a provider asks for around one whole migration run, as statements: the runner executes them itself, so every
// statement keeps the diagnostics of the runner. Acquire runs once on the run connection, outside any transaction,
// before the history table is touched. Release runs after the last unit, whatever happened, and is never cancelled.
// Bootstrap creates the history table and ReadApplied lists the versions applied so far.
//
// Exclusive says that Acquire holds a lock that keeps every other runner out until Release. The history is then
// created and read outside a transaction and a unit does not read it again. Without that lock (the default) the
// bootstrap and the read share one transaction and each unit looks its version up again inside its own transaction.
public sealed record MigrationRunPlan(
    IReadOnlyList<Statement> Acquire,
    IReadOnlyList<Statement> Release,
    IReadOnlyList<Statement> Bootstrap,
    Statement ReadApplied,
    bool Exclusive)
{
    // The plan of a provider that takes no run-wide lock.
    public static MigrationRunPlan LockFree(ISqlProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new MigrationRunPlan(
            Acquire: [],
            Release: [],
            Bootstrap: DdlCompiler.Compile(MigrationRunner.CreateHistoryTable(), provider),
            ReadApplied: StatementCompiler.Compile(MigrationRunner.ReadApplied(), provider, StatementKind.Read),
            Exclusive: false);
    }
}

using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Postgres;

// The migration port of the PostgreSQL provider. A run takes one exclusive advisory lock on its own connection before
// it touches the history table and gives it back after the last version, so two runners never apply at once: under the
// lock the history table is created and read outside a transaction, and each version runs in a transaction of its own
// without looking its version up again. Every pending operation is rendered before the first transaction begins, so a
// migration that cannot be written for this provider leaves the earlier pending ones unapplied.
internal sealed class PostgresMigrationPlanner(ISqlProvider owner) : IMigrationPlanner
{
    internal const long LockKey = 727274;

    private readonly StatementPlanner _units = new(owner);

    public void Validate(IReadOnlyList<Node> operations)
    {
        foreach (var operation in operations) DdlCompiler.Compile(operation, owner);
    }

    public MigrationRunPlan PlanRun(ISqlProvider provider) =>
        new(
            Acquire: [SessionStatement(provider, SessionLockOp.Wait)],
            Release: [SessionStatement(provider, SessionLockOp.Release)],
            Bootstrap: DdlCompiler.Compile(MigrationRunner.CreateHistoryTable(), provider),
            ReadApplied: StatementCompiler.Compile(MigrationRunner.ReadApplied(), provider, StatementKind.Read),
            Exclusive: true);

    public ValueTask<IMigrationUnit> BeginUnitAsync(
        DbConnection connection, IReadOnlyList<Node> operations, CancellationToken ct) =>
        _units.BeginUnitAsync(connection, operations, ct);

    private static Statement SessionStatement(ISqlProvider provider, SessionLockOp op) =>
        StatementCompiler.Compile(new SessionLock(op, new IntLit(LockKey)), provider, StatementKind.Read);
}

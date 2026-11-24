using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public interface IMigrationPlanner
{
    void Validate(IReadOnlyList<Node> operations);

    // The run-wide step of the planner: the exclusive lock and the history table bootstrap of one run. The default is
    // the lock-free plan, so a planner that supplies nothing still runs.
    MigrationRunPlan PlanRun(ISqlProvider provider) => MigrationRunPlan.LockFree(provider);

    ValueTask<IMigrationUnit> BeginUnitAsync(DbConnection connection, IReadOnlyList<Node> operations, CancellationToken ct);
}

public interface IMigrationUnit : IAsyncDisposable
{
    Task<IReadOnlyList<Statement>> PlanAsync(Node operation, DbTransaction transaction, CancellationToken ct);

    Task VerifyAsync(DbTransaction transaction, CancellationToken ct);
}

public sealed class StatementPlanner(ISqlProvider provider) : IMigrationPlanner
{
    public void Validate(IReadOnlyList<Node> operations)
    {
        foreach (var operation in operations) DdlCompiler.Validate(operation, provider);
    }

    public ValueTask<IMigrationUnit> BeginUnitAsync(DbConnection connection, IReadOnlyList<Node> operations, CancellationToken ct) =>
        ValueTask.FromResult<IMigrationUnit>(new StatementUnit(provider));

    private sealed class StatementUnit(ISqlProvider provider) : IMigrationUnit
    {
        public Task<IReadOnlyList<Statement>> PlanAsync(Node operation, DbTransaction transaction, CancellationToken ct) =>
            Task.FromResult(DdlCompiler.Compile(operation, provider));

        public Task VerifyAsync(DbTransaction transaction, CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

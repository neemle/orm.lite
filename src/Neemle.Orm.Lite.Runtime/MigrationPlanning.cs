using System.Data.Common;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public interface IMigrationPlanner
{
    void Validate(IReadOnlyList<Node> operations);

    ValueTask<IMigrationUnit> BeginUnitAsync(DbConnection connection, IReadOnlyList<Node> operations, CancellationToken ct);
}

public interface IMigrationUnit : IAsyncDisposable
{
    Task<IReadOnlyList<Statement>> PlanAsync(Node operation, DbTransaction transaction, CancellationToken ct);

    Task VerifyAsync(DbTransaction transaction, CancellationToken ct);
}

internal sealed class StatementPlanner(ISqlProvider provider) : IMigrationPlanner
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

using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace FaultProof;

internal sealed class FaultUnit(FaultControl control, ISqlProvider provider, bool mutating) : IMigrationUnit
{
    public Task<IReadOnlyList<Statement>> PlanAsync(Node node, DbTransaction transaction, CancellationToken ct) =>
        Task.FromResult(DdlCompiler.Compile(node, provider));
    public Task VerifyAsync(DbTransaction transaction, CancellationToken ct)
    { control.Events.Add("verify"); return Task.CompletedTask; }
    public ValueTask DisposeAsync()
    {
        control.Events.Add("unit-dispose");
        return mutating && control.Kind == FaultKind.Composite
            ? ValueTask.FromException(new IOException(FaultControl.Canary))
            : ValueTask.CompletedTask;
    }
}

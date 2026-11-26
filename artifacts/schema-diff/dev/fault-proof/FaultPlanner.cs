using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;

namespace FaultProof;

internal sealed class FaultPlanner(FaultControl control, ISqlProvider provider) : IMigrationPlanner
{
    public void Validate(IReadOnlyList<Node> operations) => PostgresProvider.Instance.Planner.Validate(operations);
    public MigrationRunPlan PlanRun(ISqlProvider owner) => PostgresProvider.Instance.Planner.PlanRun(owner);
    public ValueTask<IMigrationUnit> BeginUnitAsync(DbConnection connection, IReadOnlyList<Node> operations,
        CancellationToken ct)
    {
        control.Events.Add("unit-open");
        return ValueTask.FromResult<IMigrationUnit>(new FaultUnit(control, provider, operations.Count > 0));
    }
}

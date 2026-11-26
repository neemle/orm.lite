using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaFixtureStatements
{
    internal static void Preflight(SchemaFixturePlan plan)
    {
        var provider = plan.Context.Provider;
        foreach (var node in plan.Artifact.Recipe.Nodes.Up.Concat(plan.Artifact.Recipe.Nodes.Down))
            _ = Expected(node, provider);
        foreach (var contribution in plan.Contributions)
        {
            contribution.Require();
            var nodes = contribution.Nodes!(provider);
            plan.Planner.Validate(nodes);
            foreach (var node in nodes) _ = Expected(node, provider);
        }
        _ = SchemaStrictDdl.Compile((CreateTable)MigrationRunner.CreateHistoryTable(), provider);
        _ = DdlCompiler.Compile(MigrationRunner.RecordApplied(plan.Artifact.Version), provider);
        _ = DdlCompiler.Compile(MigrationRunner.RecordApplied(plan.Artifact.CoverageVersion), provider);
        _ = DdlCompiler.Compile(Forget(plan.Artifact.Version), provider);
    }

    internal static IReadOnlyList<Statement> Expected(Node node, ISqlProvider provider)
    {
        if (node is CreateTable create) return SchemaStrictDdl.Compile(create, provider);
        if (node is ReconcileIndexes { Indexes.Count: 0 })
        {
            try { return DdlCompiler.Compile(node, provider); }
            catch (NotSupportedException) { return Array.Empty<Statement>(); }
        }
        return DdlCompiler.Compile(node, provider);
    }

    internal static Node Forget(string version) =>
        new DeleteData(MigrationRunner.HistoryTable, MigrationRunner.VersionColumn, [new NamedParam("v", version)]);

    internal static bool Same(IReadOnlyList<Statement> left, IReadOnlyList<Statement> right) =>
        left.Count == right.Count && left.Zip(right).All(pair => Same(pair.First, pair.Second));

    private static bool Same(Statement left, Statement right) => left.Text == right.Text && left.Kind == right.Kind
        && left.Parameters.SequenceEqual(right.Parameters);
}

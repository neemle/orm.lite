using System.Data.Common;
using System.Text.RegularExpressions;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;
namespace Neemle.OrmLite.Tests.Joins;
internal sealed class JoinCompilerFixture<TMarker>
{
    internal readonly JoinExecutionProbe Probe;
    internal readonly JoinExecutionSource Source;
    internal readonly JoinCompilerProvider Provider;
    internal readonly OrmContext Context;
    internal JoinSource<JoinUnitRow<TMarker>> A = null!, B = null!;
    internal JoinSelection Selection = null!;
    internal JoinSlot<string> RootSlot = null!;
    internal JoinSlot<string?> RightSlot = null!;
    internal static JoinLimits Limits => new(4, 16, 128, 64, 10);
    internal JoinCompilerFixture(string provider = "postgres", int capacity = 100, JoinExecutionSource? source = null)
    {
        _ = JoinUnitModel<TMarker>.Descriptor;
        Source = source ?? new(new()); Probe = Source.Probe;
        Provider = new(provider == "sqlite" ? SqliteProvider.Instance : PostgresProvider.Instance, capacity);
        Context = new(Source, Provider);
        Configure(Query().IncludingDeleted(), Query().IncludingDeleted());
    }
    internal Query<JoinUnitRow<TMarker>> Query() => new(Context, JoinUnitModel<TMarker>.Map);
    internal void Configure(Query<JoinUnitRow<TMarker>> root, Query<JoinUnitRow<TMarker>> right)
    {
        A = root.AsJoinSource("alpha"); B = right.AsJoinSource("beta");
        Selection = new(); RootSlot = Selection.Add(A.Column(r => r.Name), "root");
        RightSlot = Selection.Add(B.Column(r => r.Note), "note");
    }
    internal JoinPredicate On => A.Column(r => r.Id).Equal(B.Column(r => r.Id));
    internal JoinQueryPlan Plan(bool left = true, JoinLimits? limits = null) => left
        ? JoinQueryPlan.From(A, limits ?? Limits).LeftJoin(B, On)
        : JoinQueryPlan.From(A, limits ?? Limits).InnerJoin(B, On);
    internal JoinedReadPlan Build(JoinQueryPlan? plan = null, JoinSelection? select = null) =>
        JoinedReadCompiler.Build(plan ?? Plan(), select ?? Selection);
    internal static void Error(Action action, JoinError expected) =>
        Assert.Equal(expected, Assert.Throws<JoinReadException>(action).Error);
    internal static void Sql(Statement statement, string expected, params object?[] values)
    {
        var ordered = new List<object?>(); var names = new Dictionary<string, string>();
        var sql = Regex.Replace(statement.Text, @"@p\d+", match =>
        {
            if (!names.TryGetValue(match.Value, out var alias))
            {
                alias = "@p" + names.Count; names.Add(match.Value, alias);
                ordered.Add(statement.Parameters.Single(p => "@" + p.Name == match.Value).Value);
            }
            return alias;
        });
        Assert.Equal(expected, sql); Assert.Equal(values, ordered);
        Assert.Equal(statement.Parameters.Count, statement.Parameters.Select(p => p.Ordinal).Distinct().Count());
    }
}

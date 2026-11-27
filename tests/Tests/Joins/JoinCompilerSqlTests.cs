using Xunit;
namespace Neemle.OrmLite.Tests.Joins;
[Collection("Graph")]
public sealed class JoinCompilerSqlTests
{
    [Theory] [InlineData("postgres")] [InlineData("sqlite")]
    public void Inner_parameters_and_captured_page_are_statement_scoped(string provider)
    {
        var f = new JoinCompilerFixture<JoinCompilerSqlTests>(provider);
        f.Configure(f.Query().IncludingDeleted().Where(x => x.Rank > 7).OrderBy(x => x.Rank).Skip(1).Take(2),
            f.Query().IncludingDeleted().Where(x => x.Rank < 11));
        var on = f.On.And(f.A.Column(x => x.Rank).GreaterThanValue(13));
        var plan = JoinQueryPlan.From(f.A, JoinCompilerFixture<JoinCompilerSqlTests>.Limits).InnerJoin(f.B, on)
            .Where(f.B.Column(x => x.Rank).LessThanValue(17));
        JoinCompilerFixture<JoinCompilerSqlTests>.Sql(f.Build(plan).Statement,
            "SELECT j0.\"name\" AS \"__orm_join_0\", j1.\"note\" AS \"__orm_join_1\" "
            + "FROM \"join_unit\" j0 JOIN \"join_unit\" j1 "
            + "ON ((j0.\"id\" = j1.\"id\" AND j0.\"rank\" > @p0) AND j1.\"rank\" < @p1) "
            + "WHERE j0.\"rank\" > @p2 AND j1.\"rank\" < @p3 ORDER BY j0.\"rank\" ASC LIMIT 2 OFFSET 1",
            13, 11, 7, 17);
    }
    [Theory] [InlineData("postgres")] [InlineData("sqlite")]
    public void Left_live_predicate_stays_in_on_root_live_stays_in_where(string provider)
    {
        var f = new JoinCompilerFixture<JoinCompilerSqlTests>(provider);
        f.Configure(f.Query(), f.Query());
        JoinCompilerFixture<JoinCompilerSqlTests>.Sql(f.Build().Statement,
            "SELECT j0.\"name\" AS \"__orm_join_0\", j1.\"note\" AS \"__orm_join_1\", "
            + "j1.\"id\" AS \"__orm_join_2\" FROM \"join_unit\" j0 LEFT JOIN \"join_unit\" j1 "
            + "ON (j0.\"id\" = j1.\"id\" AND j1.\"deleted_at\" IS NULL) WHERE j0.\"deleted_at\" IS NULL");
    }
    [Theory] [InlineData("postgres")] [InlineData("sqlite")]
    public void Mixed_previous_edge_chain_keeps_qualified_occurrences(string provider)
    {
        var f = new JoinCompilerFixture<JoinCompilerSqlTests>(provider);
        var c = f.Query().IncludingDeleted().AsJoinSource("third");
        var plan = f.Plan().InnerJoin(c, f.B.Column(x => x.Id).Equal(c.Column(x => x.Id)));
        JoinCompilerFixture<JoinCompilerSqlTests>.Sql(f.Build(plan).Statement,
            "SELECT j0.\"name\" AS \"__orm_join_0\", j1.\"note\" AS \"__orm_join_1\", "
            + "j1.\"id\" AS \"__orm_join_2\" FROM \"join_unit\" j0 LEFT JOIN \"join_unit\" j1 "
            + "ON j0.\"id\" = j1.\"id\" JOIN \"join_unit\" j2 ON j1.\"id\" = j2.\"id\"");
    }
    [Theory] [InlineData("postgres")] [InlineData("sqlite")]
    public void Lower_projection_and_explicit_order_replace_captured_order(string provider)
    {
        var f = new JoinCompilerFixture<JoinCompilerSqlTests>(provider);
        f.Configure(f.Query().IncludingDeleted().OrderBy(x => x.Rank), f.Query());
        var select = new JoinSelection(); select.Add(f.A.Column(x => x.Lower), "lower");
        var plan = f.Plan(false).OrderBy(f.A.Column(x => x.Name), true)
            .Where(f.A.Column(x => x.Rank).GreaterThanValue(7));
        JoinCompilerFixture<JoinCompilerSqlTests>.Sql(f.Build(plan, select).Statement,
            "SELECT lower(j0.\"name\") AS \"__orm_join_0\" FROM \"join_unit\" j0 JOIN \"join_unit\" j1 "
            + "ON (j0.\"id\" = j1.\"id\" AND j1.\"deleted_at\" IS NULL) "
            + "WHERE j0.\"rank\" > @p0 ORDER BY j0.\"name\" DESC", 7);
    }
}

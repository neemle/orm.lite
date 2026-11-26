using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph.Security;

[Collection("Graph")]
public sealed class GraphSecurityPlanTests
{
    [Theory(DisplayName = "[SEC][UC-002] quoted graph metadata compiles while hostile keys remain parameters")]
    [InlineData(false)]
    [InlineData(true)]
    public void Neutral_preflight_separates_identifiers_and_values_without_opening_data(bool postgres)
    {
        SecurityModel.Register();
        var source = new UnreachableDataSource();
        ISqlProvider provider = postgres ? PostgresProvider.Instance : SqliteProvider.Instance;
        var db = new OrmContext(source, provider);
        var request = new GraphRequest<SecurityRoot>(GraphDomain.Limits(), SecurityModel.Children);
        var plan = GraphPlanner.Build(SecurityModel.Query(db), request);
        Assert.Contains(SecurityModel.Quote(SecurityModel.RootTable), plan.RootStatement.Text);
        Assert.DoesNotContain(SecurityModel.KeyA, plan.RootStatement.Text);
        Assert.DoesNotContain(SecurityModel.KeyB, plan.RootStatement.Text);
        Assert.Contains(plan.RootStatement.Parameters, p => Equals(p.Value, SecurityModel.KeyA));
        Assert.Contains(plan.RootStatement.Parameters, p => Equals(p.Value, SecurityModel.KeyB));
        Assert.Equal(0, source.Touches);
    }

    [Fact(DisplayName = "[SEC][UC-002] only the exact trusted quoted navigation resolves without request-text echo")]
    public void An_unregistered_navigation_payload_is_not_echoed_or_admitted()
    {
        SecurityModel.Register();
        Assert.Single(SecurityModel.Children.Edges);
        var error = Assert.Throws<GraphRequestException>(() =>
            GraphPaths.Edge<SecurityRoot, SecurityLeaf>(SecurityModel.KeyA));
        Assert.Equal(GraphRequestError.InvalidPath, error.Error);
        Assert.Null(error.Path);
        Assert.DoesNotContain(SecurityModel.KeyA, error.ToString());
    }
}

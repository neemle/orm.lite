using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.Views;

public sealed class ViewInliningStateTests
{
    private static Select Reading(Node column) => new([column], new TableRef("t"));

    [Fact(DisplayName = "a named parameter in a view definition is written as a literal, like every other value, "
        + "and the statement carries no parameter")]
    public void A_named_parameter_in_a_view_is_written_inline()
    {
        var view = new CreateView("v", Reading(new NamedParam("p", 5)));

        var statements = DdlCompiler.Compile(view, PostgresProvider.Instance);

        Assert.Equal("CREATE VIEW \"v\" AS SELECT 5 FROM \"t\"", statements[0].Text);
        Assert.Empty(statements[0].Parameters);
    }

    [Fact(DisplayName = "a refusal while a view definition is written leaves the serializer binding parameters again")]
    public void A_refusal_in_a_view_does_not_leave_the_serializer_inlining()
    {
        var serializer = new PostgresSerializer();
        var refused = new CreateView("v", Reading(new Param(0, new[] { 1, 2 })));

        Assert.Throws<NotSupportedException>(() => serializer.Serialize(refused));
        var bound = serializer.Serialize(Reading(new Param(0, 5)));

        var parameter = Assert.Single(bound.Parameters);
        Assert.Equal(5, parameter.Value);
    }
}

using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Views;

// ORML-75: AsView builds a value and uses nothing of the query it is called on: no alias, no bound value.
public sealed class AsViewSideEffectTests
{
    public static TheoryData<string> Providers => ["postgres", "sqlite"];

    private static ISqlProvider Provider(string id) =>
        id == "postgres" ? PostgresProvider.Instance : SqliteProvider.Instance;

    private static Query<VwItem> Source(ISqlProvider provider)
    {
        ViewWorld.Register();
        return new MigrationBuilder(provider).From<VwItem>().Where(i => i.Status == "open");
    }

    private static void Columns(ViewColumns<VwItem, VwOpen> c) => c
        .Join<VwProject, VwItem>((i, p) => p.Id == i.ProjectId && p.Region == "eu")
        .Column(v => v.Id, i => i.Id)
        .Column(v => v.Title, i => i.Title)
        .Column<VwProject>(v => v.ProjectName, p => p.Name)
        .Column(v => v.Points, i => i.Points);

    private static void Twice(ViewColumns<VwItem, VwOpen> c) => c
        .Join<VwProject, VwItem>((i, p) => p.Id == i.ProjectId && p.Region == "eu")
        .Column(v => v.Id, i => i.Id)
        .Column(v => v.Id, i => i.Id);

    [Theory(DisplayName = "AsView called twice on one query gives equal definitions and the same marker")]
    [MemberData(nameof(Providers))]
    public void Defining_twice_gives_the_same_definition(string id)
    {
        var provider = Provider(id);
        var query = Source(provider);

        var first = query.AsView<VwOpen>(Columns);
        var second = query.AsView<VwOpen>(Columns);

        Assert.Equal(first.Query, second.Query);
        Assert.Equal(first.Marker(provider), second.Marker(provider));
    }

    [Fact(DisplayName = "AsView names the join alias s0 every time, and the joined constant stays in the definition")]
    public void The_alias_is_the_first_one_each_time()
    {
        var query = Source(PostgresProvider.Instance);

        var texts = new[] { query.AsView<VwOpen>(Columns), query.AsView<VwOpen>(Columns) }
            .Select(v => DdlCompiler.Compile(new CreateView(v.View, v.Query), PostgresProvider.Instance)[0].Text)
            .ToList();

        Assert.All(texts, text => Assert.Contains("JOIN \"vw_projects\" s0 ON", text, StringComparison.Ordinal));
        Assert.All(texts, text => Assert.Contains("= 'eu'", text, StringComparison.Ordinal));
        Assert.DoesNotContain(" s1", texts[1], StringComparison.Ordinal);
    }

    [Theory(
        DisplayName =
            "AsView binds nothing into the query: its parameter list is unchanged and a later Where takes the " +
            "next slot")]
    [MemberData(nameof(Providers))]
    public void Defining_binds_nothing_into_the_query(string id)
    {
        var query = Source(Provider(id));
        var before = query.BoundParameters.ToList();

        query.AsView<VwOpen>(Columns);
        query.Where(i => i.Points > 2);

        Assert.Single(before);
        Assert.Equal(before, query.BoundParameters.Take(1).ToList());
        Assert.Equal(2, query.BoundParameters.Count);
        Assert.Contains("@p1", query.WhereClauses[1], StringComparison.Ordinal);
    }

    [Theory(DisplayName = "a definition that throws gives back its alias and its bound value too")]
    [MemberData(nameof(Providers))]
    public void A_failed_definition_leaves_the_query_as_it_was(string id)
    {
        var provider = Provider(id);
        var query = Source(provider);
        var before = query.BoundParameters.ToList();

        Assert.Throws<InvalidOperationException>(() => query.AsView<VwOpen>(Twice));
        var after = query.AsView<VwOpen>(Columns);

        Assert.Equal(before, query.BoundParameters.ToList());
        Assert.Equal(Source(provider).AsView<VwOpen>(Columns).Marker(provider), after.Marker(provider));
    }
}

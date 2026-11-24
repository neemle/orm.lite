using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.Views;

public sealed class ViewRenderingTests
{
    private static ViewDefinition Open(ISqlProvider provider)
    {
        ViewWorld.Register();
        return new MigrationBuilder(provider).From<VwItem>()
            .Where(i => i.Status == "open" && i.Points > 2)
            .AsView<VwOpen>(c => c
                .Join<VwProject, VwItem>((i, p) => p.Id == i.ProjectId)
                .Column(v => v.Id, i => i.Id)
                .Column(v => v.Title, i => i.Title)
                .Column<VwProject>(v => v.ProjectName, p => p.Name)
                .Column(v => v.Points, i => i.Points));
    }

    private static ViewDefinition Named(string name, params string[] reads) =>
        new(name, new Select([new IntLit(1)], new TableRef(reads.FirstOrDefault() ?? "t"), Joins: NodeList.Of(reads.Skip(1).Select(r => (Node)new InnerJoin(new TableRef(r), new BoolLit(true))))));

    [Fact(DisplayName = "PostgreSQL writes a view as one CREATE statement with every value inline and a comment that carries the definition hash")]
    public void Postgres_creates_a_view_and_marks_it()
    {
        var view = Open(PostgresProvider.Instance);

        var statements = DdlCompiler.Compile(new CreateView(view.View, view.Query), PostgresProvider.Instance);

        Assert.Equal(
            "CREATE VIEW \"vw_open\" AS SELECT \"vw_items\".\"id\" AS \"id\", \"vw_items\".\"title\" AS \"title\", s0.\"name\" AS \"project_name\", \"vw_items\".\"points\" AS \"points\" "
            + "FROM \"vw_items\" JOIN \"vw_projects\" s0 ON s0.\"id\" = \"vw_items\".\"project_id\" WHERE (\"vw_items\".\"status\" = 'open' AND \"vw_items\".\"points\" > 2)",
            statements[0].Text);
        Assert.Empty(statements[0].Parameters);
        Assert.Equal("COMMENT ON VIEW \"vw_open\" IS '" + view.Marker(PostgresProvider.Instance) + "'", statements[1].Text);
        Assert.Equal(2, statements.Count);
    }

    [Fact(DisplayName = "PostgreSQL replaces a view in place and drops it with IF EXISTS")]
    public void Postgres_replaces_and_drops_a_view()
    {
        var view = Open(PostgresProvider.Instance);

        var replace = DdlCompiler.Compile(new CreateView(view.View, view.Query, true), PostgresProvider.Instance);
        var drop = DdlCompiler.Compile(new DropView("vw_open"), PostgresProvider.Instance);

        Assert.StartsWith("CREATE OR REPLACE VIEW \"vw_open\" AS SELECT ", replace[0].Text, StringComparison.Ordinal);
        Assert.Equal("DROP VIEW IF EXISTS \"vw_open\"", Assert.Single(drop).Text);
        Assert.Equal(replace[1].Text, DdlCompiler.Compile(new CreateView(view.View, view.Query), PostgresProvider.Instance)[1].Text);
    }

    [Fact(DisplayName = "SQLite has no static statement for a view that is created or replaced, because the marker and the drop belong to the migration run, and writes the drop as is")]
    public void Sqlite_plans_a_view_at_run_time()
    {
        var view = Open(SqliteProvider.Instance);

        var refusal = Assert.Throws<NotSupportedException>(() => DdlCompiler.Compile(new CreateView(view.View, view.Query), SqliteProvider.Instance));

        Assert.Contains("MigrationRunner", refusal.Message, StringComparison.Ordinal);
        Assert.Equal("DROP VIEW IF EXISTS \"vw_open\"", Assert.Single(DdlCompiler.Compile(new DropView("vw_open"), SqliteProvider.Instance)).Text);
    }

    [Fact(DisplayName = "the marker follows the definition: it is stable, differs for another definition and does not depend on whether the view is created or replaced")]
    public void The_marker_follows_the_definition()
    {
        var view = Open(PostgresProvider.Instance);
        var other = view with { Query = view.Query with { Limit = new Limit(1) } };

        Assert.Equal(view.Marker(PostgresProvider.Instance), Open(PostgresProvider.Instance).Marker(PostgresProvider.Instance));
        Assert.NotEqual(view.Marker(PostgresProvider.Instance), other.Marker(PostgresProvider.Instance));
        Assert.Equal(
            ViewDefinitions.Marker(PostgresProvider.Instance, new CreateView(view.View, view.Query)),
            ViewDefinitions.Marker(PostgresProvider.Instance, new CreateView(view.View, view.Query, true)));
    }

    [Fact(DisplayName = "a view definition lists the tables and views it reads, joins and subqueries included")]
    public void A_definition_lists_what_it_reads()
    {
        ViewWorld.Register();
        var exists = new MigrationBuilder(PostgresProvider.Instance).From<VwItem>()
            .Where(i => Db.Exists<VwProject>(p => p.Id == i.ProjectId))
            .AsView<VwBig>(c => c.Column(v => v.Id, i => i.Id).Column(v => v.Title, i => i.Title));

        Assert.Equal(["vw_items", "vw_projects"], Open(PostgresProvider.Instance).Reads.Order());
        Assert.Equal(["vw_items", "vw_projects"], exists.Reads.Order());
    }

    [Fact(DisplayName = "CreateViews orders views so that each is created after the views it reads, and DropViews is the exact reverse")]
    public void Views_are_ordered_by_dependency()
    {
        var narrow = Named("narrow", "base");
        var wide = Named("wide", "narrow");
        var widest = Named("widest", "wide", "narrow");
        var builder = new MigrationBuilder(PostgresProvider.Instance).CreateViews(widest, wide, narrow);

        Assert.Equal(["narrow", "wide", "widest"], builder.Nodes.Cast<CreateView>().Select(n => n.View));
        var drops = new MigrationBuilder(PostgresProvider.Instance).DropViews(narrow, widest, wide);
        Assert.Equal(["widest", "wide", "narrow"], drops.Nodes.Cast<DropView>().Select(n => n.View));
    }

    [Fact(DisplayName = "views that read each other, or a view named twice, are refused before any operation is recorded")]
    public void A_cycle_and_a_duplicate_are_refused()
    {
        var builder = new MigrationBuilder(PostgresProvider.Instance);

        var cycle = Assert.Throws<InvalidOperationException>(() => builder.CreateViews(Named("a", "b"), Named("b", "a")));
        var self = Assert.Throws<InvalidOperationException>(() => builder.CreateViews(Named("a", "a")));
        Assert.Throws<ArgumentException>(() => builder.CreateViews(Named("a", "t"), Named("a", "u")));

        Assert.Contains("a, b", cycle.Message, StringComparison.Ordinal);
        Assert.Contains("a", self.Message, StringComparison.Ordinal);
        Assert.Empty(builder.Nodes);
    }

    [Fact(DisplayName = "creating a view is reversible and its inverse is a drop, replacing and dropping have no derived inverse")]
    public void Views_in_the_reversibility_classifier()
    {
        var select = new Select([new IntLit(1)], new TableRef("t"));

        Assert.Equal(OpClass.Reversible, MigrationOps.ClassOf(new CreateView("v", select)));
        Assert.Equal(OpClass.Unknown, MigrationOps.ClassOf(new CreateView("v", select, true)));
        Assert.Equal(OpClass.Lossy, MigrationOps.ClassOf(new DropView("v")));
        Assert.Equal(new Node[] { new DropView("v") }, MigrationOps.DeriveDown([new CreateView("v", select)]));
        Assert.Null(MigrationOps.DeriveDown([new CreateView("v", select, true)]));
        Assert.Null(MigrationOps.DeriveDown([new DropView("v")]));
    }

    [Fact(DisplayName = "a value written into a view definition is a scalar: a collection parameter is refused, and ids, moments and enums become their stored text")]
    public void A_definition_holds_scalar_values_only()
    {
        Assert.Equal(new StringLit("00000000-0000-0000-0000-000000000001"), ParamLiterals.Of(Guid.Parse("00000000-0000-0000-0000-000000000001")));
        Assert.Equal(new StringLit("2026-03-04T05:06:07.0000000Z"), ParamLiterals.Of(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc)));
        Assert.Equal(new StringLit("Cascade"), ParamLiterals.Of(ReferentialAction.Cascade));
        Assert.Equal(new IntLit(7), ParamLiterals.Of(7));
        Assert.Equal(new NullLit(), ParamLiterals.Of(null));
        Assert.Throws<NotSupportedException>(() => ParamLiterals.Of(new[] { 1, 2 }));
    }
}

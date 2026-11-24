using System.Data.Common;
using Neemle.Orm.Lite.Nodes;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public abstract class ViewSuite<TFixture>(TFixture fixture) : MigrationSuite<TFixture>(fixture)
    where TFixture : ProviderFixture
{
    protected MigrationBuilder Builder => new(Ctx.Provider);

    protected ViewDefinition Open(params string[] statuses) =>
        Builder.From<VwItem>()
            .Where(i => statuses.Contains(i.Status))
            .AsView<VwOpen>(c => c
                .Join<VwProject, VwItem>((i, p) => p.Id == i.ProjectId)
                .Column(v => v.Id, i => i.Id)
                .Column(v => v.Title, i => i.Title)
                .Column<VwProject>(v => v.ProjectName, p => p.Name)
                .Column(v => v.Points, i => i.Points));

    private ViewDefinition OpenNow(string status) =>
        Builder.From<VwItem>()
            .Where(i => i.Status == status)
            .AsView<VwOpen>(c => c
                .Join<VwProject, VwItem>((i, p) => p.Id == i.ProjectId)
                .Column(v => v.Id, i => i.Id)
                .Column(v => v.Title, i => i.Title)
                .Column<VwProject>(v => v.ProjectName, p => p.Name)
                .Column(v => v.Points, i => i.Points));

    protected ViewDefinition Big(int points) =>
        Builder.From<VwOpen>()
            .Where(o => o.Points >= points)
            .AsView<VwBig>(c => c.Column(v => v.Id, o => o.Id).Column(v => v.Title, o => o.Title));

    protected Query<T> Read<T>(Func<DbDataReader, T> map) => new(Ctx, map);

    protected async Task ResetAsync()
    {
        ViewWorld.Register();
        await RunAsync("0900_vw_tables",
            Table("vw_projects", Col("id", ColumnType.Text, false, true), Col("name", ColumnType.Text, false), Col("region", ColumnType.Text, false)),
            Table("vw_items", Col("id", ColumnType.Text, false, true), Col("project_id", ColumnType.Text, false), Col("title", ColumnType.Text, false),
                Col("points", ColumnType.Int32, false), Col("status", ColumnType.Text, false)));
        await ApplyAsync(new DropView("vw_big"), new DropView("vw_open"), new DropView("vw_us"), new DropView("vw_totals"));
        await ExecAsync("DELETE FROM \"vw_items\"");
        await ExecAsync("DELETE FROM \"vw_projects\"");
        await ExecAsync("INSERT INTO \"vw_projects\" (\"id\", \"name\", \"region\") VALUES ('p1', 'core', 'us'), ('p2', 'billing', 'eu')");
        await ExecAsync(
            "INSERT INTO \"vw_items\" (\"id\", \"project_id\", \"title\", \"points\", \"status\") VALUES "
            + "('i1', 'p1', 'api', 5, 'open'), ('i2', 'p1', 'queue', 8, 'open'), ('i3', 'p2', 'invoice', 3, 'open'), "
            + "('i4', 'p2', 'refund', 13, 'done'), ('i5', 'p1', 'cache', 2, 'review')");
    }

    protected async Task<CatalogView?> CatalogAsync(string view)
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        return await Ctx.Provider.CreateCatalog(connection).ViewAsync(view);
    }

    private static string Titles(IEnumerable<VwOpen> rows) => string.Join(",", rows.Select(r => r.Title));

    [SkippableFact(DisplayName = "a view over a join is created from a typed query and read through Query<TView> with a filter, an order and paging, on both providers")]
    public async Task A_view_is_created_and_read_like_a_table()
    {
        Fixture.RequireAvailable();
        await ResetAsync();

        await ApplyAsync(new CreateView(Open("open").View, Open("open").Query));

        var all = await Read(ViewWorld.MapOpen).OrderBy(v => v.Points).ToListAsync();
        Assert.Equal("invoice,api,queue", Titles(all));
        Assert.Equal("billing|core|core", string.Join("|", all.Select(r => r.ProjectName)));
        Assert.Equal("api", Titles(await Read(ViewWorld.MapOpen).Where(v => v.ProjectName == "core").OrderBy(v => v.Points).Take(1).ToListAsync()));
        Assert.Equal("queue", Titles(await Read(ViewWorld.MapOpen).OrderBy(v => v.Points).Skip(2).Take(5).ToListAsync()));
        Assert.Equal(3, await Read(ViewWorld.MapOpen).CountAsync());
        Assert.Equal(13L, await Read(ViewWorld.MapOpen).SumAsync(v => v.Points) - 3);
    }

    [SkippableFact(DisplayName = "the definition hash is recorded as a marker the catalog reads back, and a changed definition reads as drift until it is replaced")]
    public async Task The_marker_detects_drift()
    {
        Fixture.RequireAvailable();
        await ResetAsync();
        var first = Open("open");
        await ApplyAsync(new CreateView(first.View, first.Query));

        var created = await CatalogAsync("vw_open");
        Assert.NotNull(created);
        Assert.Equal(first.Marker(Ctx.Provider), created.Marker);

        var changed = Open("open", "review");
        Assert.NotEqual(created.Marker, changed.Marker(Ctx.Provider));
        await ApplyAsync(new CreateView(changed.View, changed.Query, true));

        Assert.Equal(changed.Marker(Ctx.Provider), (await CatalogAsync("vw_open"))!.Marker);
        Assert.Equal(4, await Read(ViewWorld.MapOpen).CountAsync());
    }

    [SkippableFact(DisplayName = "replacing a view changes what it returns and leaves a view built on it working, on both providers")]
    public async Task A_replaced_view_keeps_its_dependents()
    {
        Fixture.RequireAvailable();
        await ResetAsync();
        var open = Open("open");
        var big = Big(5);
        await ApplyAsync(Builder.CreateViews(big, open).Nodes.ToArray());

        Assert.Equal("api,queue", string.Join(",", (await Read(ViewWorld.MapBig).OrderBy(v => v.Title).ToListAsync()).Select(r => r.Title)));

        var changed = OpenNow("done");
        await ApplyAsync(new CreateView(changed.View, changed.Query, true));

        Assert.Equal("refund", Titles(await Read(ViewWorld.MapOpen).ToListAsync()));
        Assert.Equal("refund", string.Join(",", (await Read(ViewWorld.MapBig).ToListAsync()).Select(r => r.Title)));
    }

    [SkippableFact(DisplayName = "a view is dropped with its marker, dependents first, and a missing view is not an error when IfExists holds, on both providers")]
    public async Task A_view_is_dropped()
    {
        Fixture.RequireAvailable();
        await ResetAsync();
        var open = Open("open");
        var big = Big(5);
        await ApplyAsync(Builder.CreateViews(open, big).Nodes.ToArray());
        Assert.NotNull(await CatalogAsync("vw_big"));

        await ApplyAsync(Builder.DropViews(open, big).Nodes.ToArray());

        Assert.Null(await CatalogAsync("vw_open"));
        Assert.Null(await CatalogAsync("vw_big"));
        await FailsAsync("SELECT * FROM \"vw_open\"");
        await ApplyAsync(new DropView("vw_open"));
        await ApplyAsync(new CreateView(open.View, open.Query));
        Assert.Equal(open.Marker(Ctx.Provider), (await CatalogAsync("vw_open"))!.Marker);
    }

    [SkippableFact(DisplayName = "the catalog describes the columns of a view like those of a table, on both providers")]
    public async Task The_catalog_describes_a_view()
    {
        Fixture.RequireAvailable();
        await ResetAsync();
        var open = Open("open");
        await ApplyAsync(new CreateView(open.View, open.Query));

        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        var columns = await Ctx.Provider.CreateCatalog(connection).ColumnsAsync("vw_open");

        Assert.Equal("id,title,project_name,points", string.Join(",", columns.Select(c => c.Name)));
        Assert.Equal("text,text,text,int", string.Join(",", columns.Select(c => Family(c.NativeType))));
    }

    [SkippableFact(DisplayName = "a view over an exists subquery and a view over count, sum and min aggregates are created and read, on both providers")]
    public async Task Exists_and_aggregate_views()
    {
        Fixture.RequireAvailable();
        await ResetAsync();
        var us = Builder.From<VwItem>()
            .Where(i => Db.Exists<VwProject>(p => p.Id == i.ProjectId && p.Region == "us"))
            .AsView<VwUs>(c => c.Column(v => v.Id, i => i.Id).Column(v => v.Title, i => i.Title));
        var totals = Builder.From<VwItem>()
            .Where(i => i.Status == "open")
            .AsView<VwTotals>(c => c.Count(v => v.Items).Sum(v => v.Points, i => i.Points).Min(v => v.Smallest, i => i.Points));
        await ApplyAsync(new CreateView(us.View, us.Query), new CreateView(totals.View, totals.Query));

        Assert.Equal("api,cache,queue", string.Join(",", (await Read(ViewWorld.MapUs).OrderBy(v => v.Title).ToListAsync()).Select(r => r.Title)));
        var row = Assert.Single(await Read(ViewWorld.MapTotals).ToListAsync());
        Assert.Equal((3L, 16L, 3L), (row.Items, row.Points, row.Smallest));
    }

    [SkippableFact(DisplayName = "a view over a decimal column keeps the value exact and numerically ordered, and a view summing it gives 20.29 exactly, on both providers")]
    public async Task A_view_over_a_decimal_column_stays_exact()
    {
        Fixture.RequireAvailable();
        ViewWorld.Register();
        await RunAsync("0901_vw_lines", Table("vw_lines", Col("id", ColumnType.Text, false, true),
            new ColumnDef("amount", new TypeRef(ColumnType.Decimal, 12, 4), false)));
        await ApplyAsync(new DropView("vw_line_totals"), new DropView("vw_line_view"));
        await ExecAsync("DELETE FROM \"vw_lines\"");
        await ExecAsync("INSERT INTO \"vw_lines\" (\"id\", \"amount\") VALUES ('l1', '0.1'), ('l2', '0.2'), ('l3', '9.99'), ('l4', '10')");
        var lines = Builder.From<VwLine>()
            .AsView<VwLineView>(c => c.Column(v => v.Id, l => l.Id).Column(v => v.Amount, l => l.Amount));
        var totals = Builder.From<VwLine>()
            .AsView<VwLineTotals>(c => c.Sum(v => v.Total, l => l.Amount).Min(v => v.Smallest, l => l.Amount));
        await ApplyAsync(new CreateView(lines.View, lines.Query), new CreateView(totals.View, totals.Query));

        var ordered = await Read(ViewWorld.MapLineView).OrderByDescending(v => v.Amount).ToListAsync();
        var above = await Read(ViewWorld.MapLineView).Where(v => v.Amount > 9.99m).ToListAsync();
        var total = Assert.Single(await Read(ViewWorld.MapLineTotals).ToListAsync());

        Assert.Equal(["l4", "l3", "l2", "l1"], ordered.Select(r => r.Id));
        Assert.Equal([10m, 9.99m, 0.2m, 0.1m], ordered.Select(r => r.Amount));
        Assert.Equal(["l4"], above.Select(r => r.Id));
        Assert.Equal((20.29m, 0.1m), (total.Total, total.Smallest));
    }

    [SkippableFact(DisplayName = "a view reads the rows of its base tables as they are now, so a row written after the view exists shows in it, on both providers")]
    public async Task A_view_reads_live_rows()
    {
        Fixture.RequireAvailable();
        await ResetAsync();
        var open = Open("open");
        await ApplyAsync(new CreateView(open.View, open.Query));

        await ExecAsync("INSERT INTO \"vw_items\" (\"id\", \"project_id\", \"title\", \"points\", \"status\") VALUES ('i9', 'p2', 'late', 1, 'open')");

        Assert.Equal("late,invoice,api,queue", Titles(await Read(ViewWorld.MapOpen).OrderBy(v => v.Points).ToListAsync()));
    }

    [SkippableFact(DisplayName = "a write built on a view is refused before any statement, whichever way it is built")]
    public async Task A_view_is_read_only()
    {
        Fixture.RequireAvailable();
        await ResetAsync();

        var update = Read(ViewWorld.MapOpen).Set(v => v.Title, "x").Where(v => v.Id == "i1");
        var delete = Read(ViewWorld.MapOpen).Where(v => v.Id == "i1");

        Assert.Contains("is a view", Assert.Throws<InvalidOperationException>(() => update.CompileUpdate()).Message, StringComparison.Ordinal);
        Assert.Contains("is a view", Assert.Throws<InvalidOperationException>(() => delete.CompileDelete()).Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => delete.DeleteAsync());
        Assert.True(OrmMeta.IsView(typeof(VwOpen)));
        Assert.False(OrmMeta.IsView(typeof(VwItem)));
    }

    [SkippableFact(DisplayName = "a table change that rebuilds or alters the base table keeps every view over it, definition and marker, on both providers")]
    public async Task A_table_change_keeps_the_views()
    {
        Fixture.RequireAvailable();
        await ResetAsync();
        var open = Open("open");
        var big = Big(5);
        await ApplyAsync(Builder.CreateViews(open, big).Nodes.ToArray());
        var before = await RowsAsync("SELECT \"id\" FROM \"vw_big\" ORDER BY \"id\"");

        await ApplyAsync(new AddForeignKey("fk_vw_items_project", "vw_items", Cols("project_id"), "vw_projects", Cols("id"), ReferentialAction.Cascade));

        Assert.Equal(before, await RowsAsync("SELECT \"id\" FROM \"vw_big\" ORDER BY \"id\""));
        Assert.Equal(open.Marker(Ctx.Provider), (await CatalogAsync("vw_open"))!.Marker);
        Assert.Equal(big.Marker(Ctx.Provider), (await CatalogAsync("vw_big"))!.Marker);
        await ApplyAsync(new DropForeignKey("fk_vw_items_project", "vw_items"));
        Assert.Equal(before, await RowsAsync("SELECT \"id\" FROM \"vw_big\" ORDER BY \"id\""));
    }

    [SkippableFact(DisplayName = "a view over a table with a descending, a collated and a partial unique index keeps working while the table is rebuilt or altered, and every index keeps its stored definition, on both providers")]
    public async Task A_view_over_an_indexed_table_survives_a_rebuild()
    {
        Fixture.RequireAvailable();
        await ResetAsync();
        var collation = Fixture.ProviderId == "sqlite" ? "NOCASE" : "C";
        var open = new Where(NodeList.Of([new Compare(CompareOp.Equal, new ColumnRef("status"), new StringLit("open"))]));
        var view = Open("open");
        await ApplyAsync(
            new CreateIndex("ix_vw_items_points_desc", "vw_items", NodeList.Of([IndexTerm.Desc("points").ToNode()])),
            new CreateIndex("ux_vw_items_title", "vw_items", NodeList.Of([IndexTerm.Asc("title").Collate(collation).ToNode()]), true, open),
            new CreateView(view.View, view.Query));
        var before = await IndexDefinitionsAsync();
        var rows = await RowsAsync("SELECT \"id\" FROM \"vw_open\" ORDER BY \"id\"");

        await ApplyAsync(new AddForeignKey("fk_vw_items_project", "vw_items", Cols("project_id"), "vw_projects", Cols("id"), ReferentialAction.Cascade));

        Assert.Equal(before, await IndexDefinitionsAsync());
        Assert.Equal(rows, await RowsAsync("SELECT \"id\" FROM \"vw_open\" ORDER BY \"id\""));
        Assert.Equal(view.Marker(Ctx.Provider), (await CatalogAsync("vw_open"))!.Marker);
        await FailsAsync("INSERT INTO \"vw_items\" (\"id\", \"project_id\", \"title\", \"points\", \"status\") VALUES ('i9', 'p1', 'api', 1, 'open')");
        await ExecAsync("INSERT INTO \"vw_items\" (\"id\", \"project_id\", \"title\", \"points\", \"status\") VALUES ('i9', 'p1', 'api', 1, 'done')");
        await ApplyAsync(new AddColumn("vw_items", Col("stamped_at", ColumnType.Timestamp, false, false, Defaults.Now)));
        Assert.Equal(before, await IndexDefinitionsAsync());
        await ApplyAsync(new DropColumn("vw_items", "stamped_at"), new DropForeignKey("fk_vw_items_project", "vw_items"));

        Assert.Equal(before, await IndexDefinitionsAsync());
        Assert.Equal(rows, await RowsAsync("SELECT \"id\" FROM \"vw_open\" ORDER BY \"id\""));
        var text = string.Join(" | ", before).Replace("\"", "", StringComparison.Ordinal);
        Assert.Contains("points DESC", text, StringComparison.Ordinal);
        Assert.Contains("COLLATE", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", text, StringComparison.OrdinalIgnoreCase);
        await ApplyAsync(new DropIndex("ix_vw_items_points_desc"), new DropIndex("ux_vw_items_title"));
    }

    private async Task<List<string>> IndexDefinitionsAsync()
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        var indexes = await Ctx.Provider.CreateCatalog(connection).IndexesAsync("vw_items");
        return [.. indexes.Where(i => i.Name is "ix_vw_items_points_desc" or "ux_vw_items_title").OrderBy(i => i.Name, StringComparer.Ordinal).Select(i => i.Name + ": " + i.Definition)];
    }

    [SkippableFact(DisplayName = "a view definition that leaves a column of the view unfilled, or joins a table twice, is refused when it is built")]
    public async Task A_bad_definition_is_refused()
    {
        Fixture.RequireAvailable();
        ViewWorld.Register();

        var unfilled = Assert.Throws<InvalidOperationException>(() =>
            Builder.From<VwItem>().AsView<VwOpen>(c => c.Column(v => v.Id, i => i.Id)));
        var unjoined = Assert.Throws<InvalidOperationException>(() =>
            Builder.From<VwItem>().AsView<VwOpen>(c => c.Column<VwProject>(v => v.ProjectName, p => p.Name)));
        var twice = Assert.Throws<InvalidOperationException>(() =>
            Builder.From<VwItem>().AsView<VwBig>(c => c.Column(v => v.Id, i => i.Id).Column(v => v.Id, i => i.Id)));

        Assert.Contains("title", unfilled.Message, StringComparison.Ordinal);
        Assert.Contains("VwProject", unjoined.Message, StringComparison.Ordinal);
        Assert.Contains("twice", twice.Message, StringComparison.Ordinal);
        await Task.CompletedTask;
    }
}

public sealed class SqliteViewTests(SqliteFixture fixture) : ViewSuite<SqliteFixture>(fixture)
{
    [SkippableFact(DisplayName = "SQLite drops a view that another view reads and the dependent fails only when it is read; its catalog does not report a view as a table")]
    public async Task Sqlite_resolves_a_view_when_it_is_read()
    {
        Fixture.RequireAvailable();
        await ResetAsync();
        var open = Open("open");
        var big = Big(5);
        await ApplyAsync(Builder.CreateViews(open, big).Nodes.ToArray());

        await ApplyAsync(new DropView("vw_open"));

        await FailsAsync("SELECT * FROM \"vw_big\"");
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        Assert.False(await Ctx.Provider.CreateCatalog(connection).TableExistsAsync("vw_big"));
    }
}

public sealed class PostgresViewTests(PostgresFixture fixture) : ViewSuite<PostgresFixture>(fixture)
{
    [SkippableFact(DisplayName = "PostgreSQL refuses to drop a view that another view reads and a replacement that "
        + "renames a column, and reports a view as a view, not as a table, of its catalog")]
    public async Task Postgres_protects_dependent_views()
    {
        Fixture.RequireAvailable();
        await ResetAsync();
        var open = Open("open");
        var big = Big(5);
        await ApplyAsync(Builder.CreateViews(open, big).Nodes.ToArray());

        await Assert.ThrowsAnyAsync<DbException>(() => ApplyAsync(new DropView("vw_open")));

        var renamed = open with { Query = open.Query with { Columns = NodeList.Of(open.Query.Columns.Select((c, i) => i == 1 ? (Node)new Aliased(((Aliased)c).Operand, "heading") : c)) } };
        await Assert.ThrowsAnyAsync<DbException>(() => ApplyAsync(new CreateView(renamed.View, renamed.Query, true)));
        Assert.Equal(open.Marker(Ctx.Provider), (await CatalogAsync("vw_open"))!.Marker);
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        Assert.False(await Ctx.Provider.CreateCatalog(connection).TableExistsAsync("vw_big"));
    }
}

using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

internal static class DdlBuilderCases
{
    private const string G = "ddl-builder";

    private sealed class SampleMigration : Migration
    {
        public override void Up(MigrationBuilder builder) => builder
            .CreateTable("samples", t => t
                .Column("id", ColumnType.Uuid, nullable: false, primaryKey: true)
                .Column("name", ColumnType.Text))
            .CreateIndex("ix_samples_name", "samples", "name");

        public override void Down(MigrationBuilder builder) => builder
            .DropIndex("ix_samples_name")
            .DropTable("samples");
    }

    // The cases are registered in groups, in this order; each group is one method.
    public static void Register(CorpusRun run)
    {
        var rec = run.Rec;
        RegisterTables(rec);
        RegisterColumns(rec);
        RegisterIndexesAndKeys(rec);
        RegisterData(rec);
        RegisterIdentifiersAndOrder(rec);
        RegisterViews(rec);
        RegisterViewOrder(rec);
        RegisterBuilders(rec);
    }

    private static IReadOnlyList<string> Ops(Action<MigrationBuilder> build)
    {
        var builder = new MigrationBuilder(PostgresProvider.Instance);
        build(builder);
        return builder.Render(PostgresProvider.Instance);
    }

    private static void RegisterTables(CorpusRecorder rec)
    {
        rec.Statements(G, "sql-raw-text-is-forwarded-verbatim", () => Ops(b => b
            .Raw("postgres", "SELECT 1")
            .Raw("postgres", "  CREATE EXTENSION IF NOT EXISTS pgcrypto  ")));
        rec.Statements(G, "create-table-all-column-shapes", () => Ops(b => b.CreateTable("samples", t => t
            .Column("id", ColumnType.Uuid, nullable: false, primaryKey: true)
            .Column("name", ColumnType.Text, nullable: false, defaultValue: Defaults.Text(""))
            .Column("rank", ColumnType.Int32, defaultValue: Defaults.Number(0))
            .Column("note", ColumnType.Text)
            .Column("created_at", ColumnType.Timestamp, false, false, Defaults.Now)
            .Column("odd\"name", ColumnType.Text))));
        rec.Statements(G, "create-table-with-jsonb-columns", () => Ops(b => b.CreateTable("docs", t => t
            .Column("id", ColumnType.Uuid, false, true)
            .JsonbColumn("loose")
            .JsonbColumn("strict", nullable: false))));
        rec.Statements(G, "create-table-without-columns", () => Ops(b => b.CreateTable("empty", _ => { })));
        rec.Statements(G, "drop-table", () => Ops(b => b.DropTable("samples")));
    }

    private static void RegisterColumns(CorpusRecorder rec)
    {
        rec.Statements(G, "add-column-nullable", () => Ops(b => b.AddColumn("samples", "note", ColumnType.Text)));
        rec.Statements(G, "add-column-not-null", () => Ops(b => b
            .AddColumn("samples", "note", ColumnType.Text, nullable: false)));
        rec.Statements(G, "add-column-with-default", () => Ops(b => b
            .AddColumn("samples", "rank", ColumnType.Int32, nullable: false, defaultValue: Defaults.Number(0))));
        rec.Statements(G, "add-column-nullable-with-default", () => Ops(b => b
            .AddColumn("samples", "flag", ColumnType.Bool, true, Defaults.Flag(false))));
        rec.Statements(G, "add-jsonb-column-nullable", () => Ops(b => b.AddJsonbColumn("samples", "doc")));
        rec.Statements(G, "add-jsonb-column-not-null-gets-empty-document-default", () => Ops(b => b
            .AddJsonbColumn("samples", "doc", nullable: false)));
        rec.Statements(G, "drop-column", () => Ops(b => b.DropColumn("samples", "note")));
        rec.Statements(G, "rename-column", () => Ops(b => b.RenameColumn("samples", "old_name", "new_name")));
    }

    private static void RegisterIndexesAndKeys(CorpusRecorder rec)
    {
        rec.Statements(G, "create-index-single-column", () => Ops(b => b
            .CreateIndex("ix_samples_name", "samples", "name")));
        rec.Statements(G, "create-index-composite", () => Ops(b => b
            .CreateIndex("ix_samples_name_rank", "samples", "name", "rank")));
        rec.Statements(G, "create-unique-index-single-column", () => Ops(b => b
            .CreateUniqueIndex("uq_samples_name", "samples", "name")));
        rec.Statements(G, "create-unique-index-composite", () => Ops(b => b
            .CreateUniqueIndex("uq_samples_name_rank", "samples", "name", "rank")));
        rec.Statements(G, "drop-index", () => Ops(b => b.DropIndex("ix_samples_name")));
        rec.Statements(G, "add-foreign-key", () => Ops(b => b
            .AddForeignKey("fk_samples_owner", "samples", "owner_id", "owners", "id")));
    }

    private static void RegisterData(CorpusRecorder rec)
    {
        rec.Statements(G, "insert-data-one-row", () => Ops(b => b.InsertData("samples", ["id", "name"], ["a", "b"])));
        rec.Statements(G, "insert-data-literal-kinds", () => Ops(b => b.InsertData("samples",
            ["s", "n", "d", "b", "z", "g", "l"],
            ["it's", 5, 1.5, true, null, CorpusWorld.RowA, 9L],
            ["", -1, 0.25, false, null, CorpusWorld.RowB, 0L])));
        rec.Statements(G, "insert-data-temporal-enum-and-char-kinds", () => Ops(b => b.InsertData("samples",
            ["at", "shifted", "day", "status", "c", "q", "doc"],
            [new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                new DateTimeOffset(2024, 1, 2, 10, 0, 0, new TimeSpan(5, 30, 0)),
                new DateOnly(2024, 2, 29), CorpusStatus.Active, 'x', '\'', Jsonb.Parse("{\"k\":\"it's\"}")],
            [new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Unspecified).AddTicks(1234567),
                new DateTimeOffset(2024, 1, 2, 10, 0, 0, TimeSpan.FromHours(-8)),
                new DateOnly(2024, 12, 31), CorpusStatus.Archived, 'y', 'z', Jsonb.Parse("{}")])));
        rec.Statements(G, "delete-data-guid-enum-and-date-values", () => Ops(b => b.DeleteData("samples", "k",
            CorpusWorld.RowA, CorpusStatus.Active, new DateOnly(2024, 2, 29))));
        rec.Statements(G, "delete-data-one-value", () => Ops(b => b.DeleteData("samples", "id", "a")));
        rec.Statements(G, "delete-data-several-values", () => Ops(b => b.DeleteData("samples", "rank", 1, 2, 3)));
    }

    private static void RegisterIdentifiersAndOrder(CorpusRecorder rec)
    {
        rec.Statements(G, "identifiers-are-quote-doubled", () => Ops(b => b
            .DropTable("we\"ird")
            .AddColumn("we\"ird", "c\"d", ColumnType.Text)
            .RenameColumn("t\"", "a\"", "b\"")
            .CreateIndex("i\"", "t\"", "c\"")));
        rec.Statements(G, "every-operation-in-order", () => Ops(b => b
            .Raw("postgres", "SELECT 1")
            .CreateTable("t", c => c.Column("id", ColumnType.Uuid, false, true).JsonbColumn("doc"))
            .AddColumn("t", "a", ColumnType.Text)
            .AddJsonbColumn("t", "j")
            .RenameColumn("t", "a", "b")
            .CreateIndex("ix", "t", "b")
            .CreateUniqueIndex("uq", "t", "b")
            .AddForeignKey("fk", "t", "b", "o", "id")
            .InsertData("t", ["id"], ["x"])
            .DeleteData("t", "id", "x")
            .DropIndex("uq")
            .DropColumn("t", "b")
            .DropTable("t")));
    }

    private static void RegisterViews(CorpusRecorder rec)
    {
        rec.Statements(G, "create-view-over-a-filtered-query", () => Ops(b => b.CreateView(b.From<CorpusNote>()
            .Where(n => n.Rank > 1 && n.Title == "x" && n.Status == CorpusStatus.Active)
            .AsView<CorpusView>(c => c
                .Column(v => v.Id, n => n.Id)
                .Column(v => v.Title, n => n.Title)
                .Column(v => v.Rank, n => n.Rank)))));
        rec.Statements(G, "replace-view-and-drop-view", () => Ops(b => b
            .ReplaceView(b.From<CorpusNote>().Where(n => n.Rank > 2).AsView<CorpusView>(c => c
                .Column(v => v.Id, n => n.Id)
                .Column(v => v.Title, n => n.Title)
                .Column(v => v.Rank, n => n.Rank)))
            .DropView("corpus_view")));
        rec.Statements(G, "view-over-a-join-and-an-aggregate", () => Ops(b => b
            .CreateView(b.From<CorpusNote>().AsView<CorpusView>(c => c
                .Join<CorpusTag, CorpusNote>((n, t) => t.NoteId == n.Id && t.Position == 1)
                .Column(v => v.Id, n => n.Id)
                .Column<CorpusTag>(v => v.Title, t => t.Label)
                .Column(v => v.Rank, n => n.Rank)))
            .CreateView(b.From<CorpusNote>().Where(n => n.Active).AsView<CorpusTotals>(c => c
                .Count(v => v.Items).Sum(v => v.Points, n => n.Views).Min(v => v.Smallest, n => n.Rank)))));
    }

    private static void RegisterViewOrder(CorpusRecorder rec)
    {
        rec.Statements(G, "views-in-dependency-order", () =>
        {
            var builder = new MigrationBuilder(PostgresProvider.Instance);
            var narrow = builder.From<CorpusNote>().AsView<CorpusNarrowView>(c => c.Column(v => v.Id, n => n.Id));
            var wide = builder.From<CorpusNarrowView>().AsView<CorpusView>(c => c
                .Column(v => v.Id, n => n.Id).Column(v => v.Title, n => n.Id).Column(v => v.Rank, n => n.Id));
            builder.CreateViews(wide, narrow);
            builder.DropViews(wide, narrow);
            return builder.Render(PostgresProvider.Instance);
        });
    }

    private static void RegisterBuilders(CorpusRecorder rec)
    {
        rec.Statements(G, "explicit-ddl-dialect-constructor", () =>
        {
            var builder = new MigrationBuilder(PostgresProvider.Instance);
            builder.CreateTable("t", _ => { }).DropTable("t");
            return builder.Render(PostgresProvider.Instance);
        });
        rec.Text(G, "table-builder-standalone-render", () =>
        {
            var table = new TableBuilder(PostgresProvider.Instance).Column("id", ColumnType.Uuid, false, true)
                .Column("x", ColumnType.Int32, true, false, Defaults.Number(1)).JsonbColumn("j", false);
            var viaProvider = new TableBuilder(PostgresProvider.Instance).Column("y", ColumnType.Text);
            return CorpusFormat.Lines(
                [table.Render(), viaProvider.Render(), new TableBuilder(PostgresProvider.Instance).Render()]);
        });
        rec.Statements(G, "migration-up-and-down-bodies", () =>
        {
            Migration migration = new SampleMigration();
            var up = new MigrationBuilder(PostgresProvider.Instance);
            migration.Up(up);
            var down = new MigrationBuilder(PostgresProvider.Instance);
            migration.Down(down);
            return up.Render(PostgresProvider.Instance).Select(o => "up: " + o)
                .Concat(down.Render(PostgresProvider.Instance).Select(o => "down: " + o));
        });
    }
}

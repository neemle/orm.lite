namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

internal static class RowHistoryCases
{
    private const string G = "row-history";

    private const string H = "hash";

    private const string DefaultActor = OrmLiteSettings.DefaultActorSetting;

    private static readonly ISqlProvider Pg = Neemle.Orm.Lite.Postgres.PostgresProvider.Instance;

    public static void Register(CorpusRun run)
    {
        var rec = run.Rec;
        var tenantTable = new RowHistoryTable("corpus_tasks", "id", "deleted_at", "tenant_id", ["updated_at"]);
        var platformTable = new RowHistoryTable("corpus_platform_items", "id", "deleted_at", "", []);
        var plainTable = new RowHistoryTable("corpus_projects", "id", "deleted_at", "id", ["a", "b"]);
        var task = CorpusWorld.Soft<CorpusTask>();
        var item = CorpusWorld.Soft<CorpusPlatformItem>();
        var project = CorpusWorld.Soft<CorpusProject>();
        var bare = CorpusWorld.Soft<CorpusBare>();
        var noKey = CorpusWorld.Soft<CorpusNoKey>();

        rec.Text(G, "names-and-prefixes", () => CorpusFormat.Lines(
        [
            RowHistoryDdl.TableName,
            RowHistoryDdl.VersionPrefix,
            RowHistoryDdl.FunctionName,
            RowHistoryDdl.UpdateTrigger,
            RowHistoryDdl.DeleteTrigger,
            OrmLiteSettings.DefaultActorSetting,
            OrmLiteSettings.ActorSetting,
            Neemle.Orm.Lite.Postgres.PostgresActorStamp.Statement,
            Neemle.Orm.Lite.Postgres.PostgresActorStamp.Statement,
        ]));
        rec.Statements(G, "ddl-preamble-under-the-default-actor-setting", () => RowHistoryDdl.PreambleFor(Pg, DefaultActor));
        rec.Text(G, "ddl-capture-function-under-the-default-actor-setting", () => RowHistoryDdl.FunctionSqlFor(Pg, DefaultActor) + "\n");
        rec.Text(G, "ddl-capture-function-under-a-custom-actor-setting", () => RowHistoryDdl.FunctionSqlFor(Pg, "app.actor") + "\n");
        rec.Statements(G, "ddl-reconcile-without-tables", () => RowHistoryDdl.ReconcileFor(Pg, DefaultActor, []));
        rec.Statements(G, "ddl-reconcile-tenant-table", () => RowHistoryDdl.ReconcileFor(Pg, DefaultActor, [tenantTable]).Skip(RowHistoryDdl.PreambleFor(Pg, DefaultActor).Length));
        rec.Statements(G, "ddl-reconcile-platform-table", () => RowHistoryDdl.ReconcileFor(Pg, DefaultActor, [platformTable]).Skip(RowHistoryDdl.PreambleFor(Pg, DefaultActor).Length));
        rec.Statements(G, "ddl-reconcile-tenant-is-key-table-with-unversioned-columns", () => RowHistoryDdl.ReconcileFor(Pg, DefaultActor, [plainTable]).Skip(RowHistoryDdl.PreambleFor(Pg, DefaultActor).Length));
        rec.Statements(G, "ddl-reconcile-several-tables-keep-their-order", () => RowHistoryDdl.ReconcileFor(Pg, DefaultActor, [platformTable, tenantTable, plainTable]).Skip(RowHistoryDdl.PreambleFor(Pg, DefaultActor).Length));
        rec.Statements(G, "ddl-reconcile-public-entry-point", () => RowHistoryDdl.Reconcile(Pg, [tenantTable]).Skip(RowHistoryDdl.PreambleFor(Pg, DefaultActor).Length));
        rec.Refusal(G, "ddl-reconcile-table-not-an-identifier", () => RowHistoryDdl.Reconcile(Pg, [new RowHistoryTable("Bad Table", "id", "deleted_at", "", [])]));
        rec.Refusal(G, "ddl-reconcile-tenant-not-an-identifier", () => RowHistoryDdl.Reconcile(Pg, [new RowHistoryTable("t", "id", "deleted_at", "Tenant", [])]));
        rec.Refusal(G, "ddl-reconcile-unversioned-not-an-identifier", () => RowHistoryDdl.Reconcile(Pg, [new RowHistoryTable("t", "id", "deleted_at", "", ["x y"])]));
        rec.Refusal(G, "ddl-function-with-an-invalid-actor-setting", () => RowHistoryDdl.FunctionSqlFor(Pg, "Bad"));

        rec.Text(G, "read-state-sql-without-tenant", () => RowHistoryRead.RenderStateSql(Pg, "corpus_platform_items", "id", []) + "\n");
        rec.Text(G, "read-state-sql-with-tenant-and-unversioned", () => RowHistoryRead.RenderStateSql(Pg, "corpus_tasks", "id", ["updated_at", "doc"], "tenant_id") + "\n");
        rec.Text(G, "read-versions-sql-without-tenant", () => RowHistoryRead.RenderVersionsSql(Pg, "corpus_platform_items", null, null) + "\n");
        rec.Text(G, "read-versions-sql-with-tenant", () => RowHistoryRead.RenderVersionsSql(Pg, "corpus_tasks", "id", "tenant_id") + "\n");
        rec.Text(G, "read-exists-sql-without-tenant", () => RowHistoryRead.RenderExistsSql(Pg, "corpus_platform_items", "id", null) + "\n");
        rec.Text(G, "read-exists-sql-with-tenant", () => RowHistoryRead.RenderExistsSql(Pg, "corpus_tasks", "id", "tenant_id") + "\n");
        rec.Refusal(G, "read-state-sql-table-not-an-identifier", () => RowHistoryRead.RenderStateSql(Pg, "Bad", "id", []));
        rec.Refusal(G, "read-state-sql-unversioned-not-an-identifier", () => RowHistoryRead.RenderStateSql(Pg, "t", "id", ["bad name"]));
        rec.Refusal(G, "read-versions-sql-tenant-not-an-identifier", () => RowHistoryRead.RenderVersionsSql(Pg, "t", "id", "Tenant"));
        rec.Refusal(G, "read-exists-sql-key-not-an-identifier", () => RowHistoryRead.RenderExistsSql(Pg, "t", "Id", null));

        rec.Text(G, "revert-sql-tenant-entity-without-status", () => RowHistoryRevert.RenderSql(Pg, task, false) + "\n");
        rec.Text(G, "revert-sql-tenant-entity-with-status", () => RowHistoryRevert.RenderSql(Pg, task, true) + "\n");
        rec.Text(G, "revert-sql-platform-entity", () => RowHistoryRevert.RenderSql(Pg, item, false) + "\n");
        rec.Text(G, "revert-sql-tenant-is-key-entity", () => RowHistoryRevert.RenderSql(Pg, project, true) + "\n");
        rec.Refusal(G, "revert-sql-with-nothing-to-revert", () => RowHistoryRevert.RenderSql(Pg, bare, true));
        rec.Refusal(G, "revert-sql-null-entry", () => RowHistoryRevert.RenderSql(Pg, null!, true));
        rec.Statements(G, "revert-columns-without-status", () => RowHistoryRevert.RevertedColumns(task, false));
        rec.Statements(G, "revert-columns-with-status", () => RowHistoryRevert.RevertedColumns(task, true));
        rec.Statements(G, "revert-columns-platform-entity", () => RowHistoryRevert.RevertedColumns(item, false));

        rec.Text(G, "trash-union-default-filter", () => Union([task, item, project, noKey], new SoftTrashFilter()));
        rec.Text(G, "trash-union-including-platform", () => Union([task, item, project, noKey], new SoftTrashFilter(IncludePlatform: true)));
        rec.Text(G, "trash-union-one-entity-token", () => Union([task, item, project], new SoftTrashFilter(EntityToken: "corpus_task")));
        rec.Text(G, "trash-union-project-filter", () => Union([task, item, project], new SoftTrashFilter(ProjectId: CorpusWorld.RowA)));
        rec.Text(G, "trash-union-project-filter-including-platform", () => Union([task, item, project], new SoftTrashFilter(ProjectId: CorpusWorld.RowA, IncludePlatform: true)));
        rec.Text(G, "trash-union-token-and-project-filters", () => Union([task, project], new SoftTrashFilter("project", CorpusWorld.RowA, true)));
        rec.Text(G, "trash-union-nothing-matches", () => Union([task, item], new SoftTrashFilter(EntityToken: "nope")));
        rec.Text(G, "trash-union-no-entries", () => Union([], new SoftTrashFilter()));
        rec.Text(G, "trash-helpers", () => CorpusFormat.Lines(
        [
            "LabelMaxChars = " + SoftTrashRead.LabelMaxChars,
            "Quote(plain) = " + SoftTrashRead.Quote("corpus_tasks"),
            "ProjectColumnOf(task) = " + (SoftTrashRead.ProjectColumnOf(task) ?? "null"),
            "ProjectColumnOf(platform item) = " + (SoftTrashRead.ProjectColumnOf(item) ?? "null"),
            "ProjectColumnOf(project) = " + (SoftTrashRead.ProjectColumnOf(project) ?? "null"),
        ]));
        rec.Refusal(G, "trash-quote-not-an-identifier", () => SoftTrashRead.Quote("Bad Name"));
        rec.Refusal(G, "trash-union-label-not-an-identifier", () => SoftTrashRead.RenderUnionSql(Pg, [new BadTableEntry(task)], new SoftTrashFilter()));

        rec.Text(H, "statements-hash-vectors", () => CorpusFormat.Lines(
        [
            "[] = " + RowHistoryDdl.StatementsHash([]),
            "[\"\"] = " + RowHistoryDdl.StatementsHash([""]),
            "[\"a\"] = " + RowHistoryDdl.StatementsHash(["a"]),
            "[\"a\", \"b\"] = " + RowHistoryDdl.StatementsHash(["a", "b"]),
            "[\"b\", \"a\"] = " + RowHistoryDdl.StatementsHash(["b", "a"]),
            "[\"a\\0b\"] = " + RowHistoryDdl.StatementsHash(["a\0b"]),
            "[\"ab\"] = " + RowHistoryDdl.StatementsHash(["ab"]),
            "[\"\", \"\"] = " + RowHistoryDdl.StatementsHash(["", ""]),
            "[non-ascii] = " + RowHistoryDdl.StatementsHash(["é中"]),
        ]));
        rec.Text(H, "arguments-hash-vectors", () => CorpusFormat.Lines(
        [
            "tenant table = " + RowHistoryDdl.ArgumentsHash(tenantTable),
            "platform table = " + RowHistoryDdl.ArgumentsHash(platformTable),
            "tenant-is-key table = " + RowHistoryDdl.ArgumentsHash(plainTable),
            "table name is not part of the hash = " + RowHistoryDdl.ArgumentsHash(tenantTable with { Table = "other_table" }),
            "unversioned order is part of the hash = " + RowHistoryDdl.ArgumentsHash(plainTable with { Unversioned = ["b", "a"] }),
        ]));
        rec.Text(H, "set-hash-vectors", () => CorpusFormat.Lines(
        [
            "no tables = " + RowHistoryDdl.SetHash([]),
            "tenant table = " + RowHistoryDdl.SetHash([tenantTable]),
            "platform table = " + RowHistoryDdl.SetHash([platformTable]),
            "tenant then platform = " + RowHistoryDdl.SetHash([tenantTable, platformTable]),
            "platform then tenant = " + RowHistoryDdl.SetHash([platformTable, tenantTable]),
            "version name formula = " + RowHistoryDdl.VersionPrefix + "Assembly_" + RowHistoryDdl.SetHash([tenantTable]),
        ]));
        rec.Text(H, "set-hash-equals-statements-hash-of-reconcile", () =>
        {
            var statements = RowHistoryDdl.Reconcile(Pg, [tenantTable]);
            return CorpusFormat.Lines([RowHistoryDdl.SetHash([tenantTable]) + " == " + RowHistoryDdl.StatementsHash(statements) + " : " + (RowHistoryDdl.SetHash([tenantTable]) == RowHistoryDdl.StatementsHash(statements))]);
        });
    }

    private static string Union(IReadOnlyList<ISoftEntry> entries, SoftTrashFilter filter) =>
        (SoftTrashRead.RenderUnionSql(Pg, entries, filter) ?? "null") + "\n";

    private sealed class BadTableEntry(ISoftEntry inner) : ISoftEntry
    {
        public Type ClrType => inner.ClrType;
        public string Table => "Bad Table";
        public string EntityToken => inner.EntityToken;
        public string KeyColumn => inner.KeyColumn;
        public string SoftColumn => inner.SoftColumn;
        public string? TenantColumn => inner.TenantColumn;
        public bool Platform => inner.Platform;
        public string? LabelColumn => inner.LabelColumn;
        public IReadOnlyList<string> Owners => inner.Owners;
        public IReadOnlyList<string> Unversioned => inner.Unversioned;
        public IReadOnlyList<string> Pinned => inner.Pinned;
        public IReadOnlyList<string> StatusColumns => inner.StatusColumns;
        public string? TypeColumnOfOwner(string ownerColumn) => inner.TypeColumnOfOwner(ownerColumn);
        public string? JsonNameOfColumn(string column) => inner.JsonNameOfColumn(column);
        public string? ColumnOfJsonName(string jsonName) => inner.ColumnOfJsonName(jsonName);
        public TResult Accept<TResult>(ISoftEntryVisitor<TResult> visitor) => inner.Accept(visitor);
    }
}

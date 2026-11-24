using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

internal static class SoftDeleteCases
{
    private const string G = "soft-delete";

    public static void Register(CorpusRun run)
    {
        var a = CorpusWorld.RowA;
        var b = CorpusWorld.RowB;
        var tenant = CorpusWorld.Tenant;
        var ids = new List<Guid> { a, b };

        run.Rec.Statement(G, "purge-by-predicate-includes-deleted", () =>
        {
            var q = run.Q<CorpusTask>().Where(t => t.Rank > 1);
            return CorpusRun.Read(q, q.CompilePurge(), q.RenderPurge());
        });
        run.Rec.Statement(G, "purge-by-predicate-plain-entity", () =>
        {
            var q = run.Q<CorpusNote>().Where(n => n.Rank > 1);
            return CorpusRun.Read(q, q.CompilePurge(), q.RenderPurge());
        });
        run.Rec.Statement(G, "purge-by-key-tenant", () =>
        {
            var q = run.Q<CorpusTask>();
            return CorpusRun.Keyed(q, q.CompilePurge(ids, tenant), q.RenderPurge(ids, tenant));
        });
        run.Rec.Statement(G, "purge-by-key-platform", () =>
        {
            var q = run.Q<CorpusPlatformItem>();
            return CorpusRun.Keyed(q, q.CompilePurge(ids, null), q.RenderPurge(ids, null));
        });
        run.Rec.Statement(G, "purge-by-key-tenant-is-key", () =>
        {
            var q = run.Q<CorpusProject>();
            return CorpusRun.Keyed(q, q.CompilePurge(ids, tenant), q.RenderPurge(ids, tenant));
        });
        run.Rec.Statement(G, "restore-tenant", () =>
        {
            var q = run.Q<CorpusTask>();
            return CorpusRun.Keyed(q, q.CompileRestore(ids, tenant), q.RenderRestore(ids, tenant));
        });
        run.Rec.Statement(G, "restore-platform", () =>
        {
            var q = run.Q<CorpusPlatformItem>();
            return CorpusRun.Keyed(q, q.CompileRestore(ids, null), q.RenderRestore(ids, null));
        });
        run.Rec.Statement(G, "restore-tenant-is-key", () =>
        {
            var q = run.Q<CorpusProject>();
            return CorpusRun.Keyed(q, q.CompileRestore(ids, tenant), q.RenderRestore(ids, tenant));
        });
        run.Rec.Statement(G, "restore-after-bound-parameters", () =>
        {
            var q = run.Q<CorpusTask>().Where(t => t.Rank > 1);
            return CorpusRun.Keyed(q, q.CompileRestore(ids, tenant), q.RenderRestore(ids, tenant));
        });

        run.Rec.Text(G, "delete-by-key-statement", () =>
        {
            var descriptor = EntityDescriptor.Of("global::Corpus.Task", "corpus_tasks", true)
                .Property("Id", "id", ColumnType.Uuid, false, true)
                .Property("DeletedAt", "deleted_at", ColumnType.Timestamp, true, false)
                .SoftDelete("DeletedAt", "deleted_at", "task");
            return StatementCompiler.Compile(EntityStatements.Delete(descriptor, Guid.Empty), PostgresProvider.Instance, StatementKind.Write).Text + "\n";
        });
        run.Rec.Text(G, "soft-delete-fragments", () =>
        {
            var pg = new PgDialect();
            return CorpusFormat.Lines(
            [
                SoftDeleteSql.SetClause(pg, "deleted_at"),
                SoftDeleteSql.LiveTerm(pg, "deleted_at"),
                SoftDeleteSql.LiveTerm(pg, "deleted_at", "s0"),
                SoftDeleteSql.LiveTerm(pg, "we\"ird", "\"t\""),
            ]);
        });
        run.Rec.Text(G, "soft-registry-metadata", () =>
        {
            var lines = new List<string>();
            foreach (var entry in new[] { CorpusWorld.Soft<CorpusTask>(), CorpusWorld.Soft<CorpusPlatformItem>(), CorpusWorld.Soft<CorpusProject>() })
            {
                lines.Add(entry.Table + " token=" + entry.EntityToken + " key=" + entry.KeyColumn + " soft=" + entry.SoftColumn
                    + " tenant=" + (entry.TenantColumn ?? "null") + " platform=" + entry.Platform + " label=" + (entry.LabelColumn ?? "null")
                    + " owners=[" + string.Join(",", entry.Owners) + "] unversioned=[" + string.Join(",", entry.Unversioned)
                    + "] pinned=[" + string.Join(",", entry.Pinned) + "] status=[" + string.Join(",", entry.StatusColumns) + "]");
                lines.Add(entry.Table + " json-of-updated-at=" + (entry.JsonNameOfColumn("updated_at") ?? "null") + " column-of-json-updatedAt=" + (entry.ColumnOfJsonName("updatedAt") ?? "null")
                    + " type-column-of-project_id=" + (entry.TypeColumnOfOwner("project_id") ?? "null"));
            }
            return CorpusFormat.Lines(lines);
        });
    }
}

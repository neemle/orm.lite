using System.Data.Common;
using System.Reflection;
using System.Text;
using Neemle.OrmLite;
using Neemle.OrmLite.Postgres;

namespace CorpusProbe;

public static class Probe
{
    private const string Generated = "Neemle.OrmLite.Generated.";

    static Probe() => ProviderRegistry.Register(Neemle.OrmLite.Postgres.PostgresProvider.Instance);

    public static string Dump(string token)
    {
        var assembly = typeof(Probe).Assembly;
        System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        var sb = new StringBuilder();
        var schema = (OrmSchema)assembly.GetType(Generated + token + "Schema", throwOnError: true)!.GetField("Instance")!.GetValue(null)!;
        sb.Append("-- schema\n");
        sb.Append("CreateAll: ").Append(string.Join("; ", DdlCompiler.Render(schema.Baseline(), PostgresProvider.Instance))).Append('\n');
        Lines(sb, "CreateIndexes", DdlCompiler.Render(schema.Indexes(), PostgresProvider.Instance));
        Lines(sb, "ReconcileIndexes", DdlCompiler.Render([schema.Reconcile()], PostgresProvider.Instance));
        sb.Append("IndexSetHash: ").Append(schema.IndexSetHash).Append('\n');
        if (schema.HasSoftDeletion)
        {
            var historyTables = schema.HistoryTables;
            sb.Append("HistoryTables:\n");
            foreach (var table in historyTables)
                sb.Append("  ").Append(table.Table).Append(" key=").Append(table.KeyColumn).Append(" soft=").Append(table.SoftColumn)
                    .Append(" tenant=").Append(table.TenantColumn.Length == 0 ? "<none>" : table.TenantColumn)
                    .Append(" unversioned=[").Append(string.Join(",", table.Unversioned)).Append("] arguments-hash=").Append(RowHistoryDdl.ArgumentsHash(table)).Append('\n');
            sb.Append("HistorySetHash: ").Append(RowHistoryDdl.SetHash(historyTables)).Append('\n');
        }
        else sb.Append("HistoryTables: <not emitted>\n");

        sb.Append("-- contributions\n");
        var migrations = assembly.GetType(Generated + token + "Migrations", throwOnError: true)!;
        var contributions = (MigrationContribution[])migrations.GetField("Contributions")!.GetValue(null)!;
        foreach (var c in contributions)
        {
            sb.Append("version ").Append(c.Version).Append(" reversibility=").Append(c.Reversibility).Append(" down=").Append(c.Down is null ? "none" : "declared").Append('\n');
            Lines(sb, "  up", c.Render(PostgresProvider.Instance));
            if (c.Down is not null)
            {
                var down = new MigrationBuilder(PostgresProvider.Instance);
                c.Down(down);
                Lines(sb, "  down", down.Render(PostgresProvider.Instance));
            }
        }

        sb.Append("-- registry\n");
        var tables = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var type in assembly.GetTypes().Where(t => t.Namespace == "Neemle.OrmLite.Generated" && t.Name.EndsWith("Orm", StringComparison.Ordinal)))
            if (type.GetField("Table") is { IsLiteral: true } field) tables.Add((string)field.GetRawConstantValue()!);
        var soft = OrmMeta.SoftEntries().ToDictionary(e => e.Table, e => e.Meta, StringComparer.Ordinal);
        var typed = OrmMeta.SoftTypedEntries().ToDictionary(e => e.Table, e => e, StringComparer.Ordinal);
        foreach (var table in tables)
        {
            sb.Append(table).Append(" columns=[").Append(string.Join(",", OrmMeta.ColumnsForTable(table) ?? [])).Append("]\n");
            if (!soft.TryGetValue(table, out var meta)) continue;
            sb.Append("  soft column=").Append(meta.Column).Append(" tenant=").Append(meta.TenantColumn ?? "null").Append(" platform=").Append(meta.Platform)
                .Append(" key=").Append(meta.KeyColumn ?? "null").Append(" label=").Append(meta.LabelColumn ?? "null").Append(" token=").Append(meta.EntityToken ?? "null").Append('\n');
            sb.Append("  live-only=[").Append(string.Join(";", (meta.LiveOnlyIndexes ?? []).Select(i => string.Join(",", i)))).Append("] unversioned=[").Append(string.Join(",", meta.Unversioned ?? []))
                .Append("] owners=[").Append(string.Join(",", meta.Owners ?? [])).Append("] pinned=[").Append(string.Join(",", meta.PinnedColumns ?? [])).Append("] status=[").Append(string.Join(",", meta.StatusColumns ?? [])).Append("]\n");
            sb.Append("  property-columns=[").Append(string.Join(";", (meta.PropertyColumns ?? []).Select(p => p[0] + ">" + p[1]))).Append("]\n");
            var entry = typed[table];
            sb.Append("  typed pinned=[").Append(string.Join(",", entry.Pinned)).Append("] owners=[").Append(string.Join(",", entry.Owners)).Append("] soft-column=").Append(entry.SoftColumn).Append('\n');
        }
        return sb.ToString();
    }

    public static string[] Versions(string token)
    {
        var migrations = typeof(Probe).Assembly.GetType(Generated + token + "Migrations", throwOnError: true)!;
        var contributions = (MigrationContribution[])migrations.GetField("Contributions")!.GetValue(null)!;
        return contributions.Select(c => c.Version).OrderBy(v => v, StringComparer.Ordinal).ToArray();
    }

    public static Task RunMigrations(DbDataSource dataSource, string token)
    {
        var migrations = typeof(Probe).Assembly.GetType(Generated + token + "Migrations", throwOnError: true)!;
        return MigrationRunner.RunAsync(dataSource, PostgresProvider.Instance, (MigrationContribution[])migrations.GetField("Contributions")!.GetValue(null)!);
    }

    private static void Lines(StringBuilder sb, string label, IEnumerable<string> lines)
    {
        sb.Append(label).Append(":\n");
        foreach (var line in lines) sb.Append("    ").Append(line.Replace("\n", "\n    ")).Append('\n');
    }
}

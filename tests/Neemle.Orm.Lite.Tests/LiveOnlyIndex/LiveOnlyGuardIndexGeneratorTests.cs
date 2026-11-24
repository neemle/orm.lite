using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.LiveOnlyIndex;

public sealed class LiveOnlyGuardIndexGeneratorTests
{
    private static string Entity(string indexes) =>
        $"[Table(\"live_only_scoped\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform)] {indexes} public class Doc "
        + "{ [Key] public Guid Id { get; set; } public string Scope { get; set; } = \"\"; public string Name { get; set; } = \"\"; public string Code { get; set; } = \"\"; public DateTime? DeletedAt { get; set; } }\n";

    private const string LiveAndPlain = "[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true)] [Index(\"Scope\", \"Code\", Unique = true)]";

    private const string PlainReconcileElement = """DO $reconcile$ BEGIN IF to_regclass('"live_only_scoped"') IS NOT NULL AND (SELECT count(*) FROM pg_attribute WHERE attrelid = to_regclass('"live_only_scoped"') AND attnum > 0 AND NOT attisdropped AND attname = ANY (ARRAY['scope', 'code'])) = 2 THEN EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS "ix_live_only_scoped_scope_code" ON "live_only_scoped" ("scope", "code")'; END IF; END $reconcile$""";

    private static readonly string[] CreateIndexes =
    [
        """CREATE UNIQUE INDEX IF NOT EXISTS "ix_live_only_scoped_scope_name_live" ON "live_only_scoped" ("scope", "name") WHERE "deleted_at" IS NULL""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "ix_live_only_scoped_scope_code" ON "live_only_scoped" ("scope", "code")""",
    ];

    private const string IndexSetHashValue = "00366bb721fcbbe7";

    private const string LiveReconcileElement = """DO $reconcile$ BEGIN IF to_regclass('"live_only_scoped"') IS NOT NULL AND (SELECT count(*) FROM pg_attribute WHERE attrelid = to_regclass('"live_only_scoped"') AND attnum > 0 AND NOT attisdropped AND attname = ANY (ARRAY['scope', 'name', 'deleted_at'])) = 3 THEN EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS "ix_live_only_scoped_scope_name_live" ON "live_only_scoped" ("scope", "name") WHERE "deleted_at" IS NULL'; END IF; END $reconcile$""";

    private static IReadOnlyList<string> Reconcile(Microsoft.CodeAnalysis.Compilation output) =>
        GeneratedModel.Render([GeneratedModel.Of(output).Schema.Reconcile()]);

    [Fact(DisplayName = "AC1 the reconcile guard of a LiveOnly index on a soft entity checks the key columns plus the soft column and compares the count of that set")]
    public void The_live_only_guard_names_the_soft_column()
    {
        var (diagnostics, _, output) = Run(Entity(LiveAndPlain));

        Assert.Empty(diagnostics);
        Assert.Contains(LiveReconcileElement, Reconcile(output));
    }

    [Fact(DisplayName = "AC6 a LiveOnly index that already lists the soft column names it once and compares a count equal to the distinct names")]
    public void The_soft_column_is_not_duplicated_in_the_guard()
    {
        var (diagnostics, _, output) = Run(Entity("[Index(\"Scope\", \"DeletedAt\", Unique = true, LiveOnly = true)]"));

        Assert.Empty(diagnostics);
        var reconcile = string.Join("\n", Reconcile(output));
        Assert.Contains("ARRAY['scope', 'deleted_at'])) = 2 THEN", reconcile);
        Assert.DoesNotContain("'deleted_at', 'deleted_at'", reconcile);
    }

    [Fact(DisplayName = "AC2 the reconcile statement of a plain index, the createIndexes text and the index-set hash on a soft entity are unchanged by the live-only guard")]
    public void Non_live_only_output_is_unchanged()
    {
        var (diagnostics, _, output) = Run(Entity(LiveAndPlain));

        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        Assert.Contains(PlainReconcileElement, GeneratedModel.Render([model.Schema.Reconcile()]));
        Assert.Equal(CreateIndexes, GeneratedModel.Render(model.Schema.Indexes()));
        Assert.Equal(IndexSetHashValue, model.Schema.IndexSetHash);
    }
}

using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Tests.Settings;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Nodes;

[Collection(ActorSettingCollection.Name)]
public sealed class OrmSchemaTests
{
    private sealed class Seed : Migration
    {
        public override void Up(MigrationBuilder b) => b.DropTable("seeds");

        public override void Down(MigrationBuilder b) => b.DropTable("seeds_down");
    }

    private static EntityDescriptor Doc() =>
        EntityDescriptor.Of("global::T.Doc", "docs", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("Name", "name", ColumnType.Text, false, false)
            .Property("DeletedAt", "deleted_at", ColumnType.Timestamp, true, false)
            .Index("ix_docs_name", true, true, "name")
            .SoftDelete("DeletedAt", "deleted_at", "doc");

    private static EntityDescriptor Plain() =>
        EntityDescriptor.Of("global::T.Plain", "plains", true).Property("Id", "id", ColumnType.Uuid, false, true);

    [Fact(DisplayName = "the contributions of a schema are the baseline, the indexes, the user migrations in version order, the reconcile and the history, each under its derived version")]
    public void The_contributions_are_ordered_and_named()
    {
        var schema = OrmSchema.Of("Tok", Doc());
        using var scope = ActorSettingScope.Fresh();

        var all = schema.Contributions(
            MigrationContribution.For<Seed>("0200_b", Reversibility.Reversible, true),
            MigrationContribution.For<Seed>("0100_a", Reversibility.Reversible, false));

        Assert.Equal(
            new[]
            {
                "0000_baseline_Tok", "0001_orm_indexes_Tok", "0100_a", "0200_b",
                "9900_orm_index_reconcile_Tok_" + schema.IndexSetHash,
                RowHistoryDdl.VersionPrefix + "Tok_" + RowHistoryDdl.SetHash(schema.HistoryTables),
            },
            all.Select(c => c.Version));
        Assert.Equal(
            new[]
            {
                Reversibility.Irreversible, Reversibility.Reversible, Reversibility.Reversible,
                Reversibility.Reversible, Reversibility.Irreversible, Reversibility.Irreversible,
            },
            all.Select(c => c.Reversibility));
    }

    [Fact(DisplayName = "an assembly with no soft entity has no history contribution and no history table")]
    public void A_plain_schema_has_no_history()
    {
        var schema = OrmSchema.Of("Tok", Plain());

        Assert.False(schema.HasSoftDeletion);
        Assert.Empty(schema.HistoryTables);
        Assert.Equal(3, schema.Contributions().Length);
        Assert.DoesNotContain(schema.Contributions(), c => c.Version.StartsWith(RowHistoryDdl.VersionPrefix, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "the baseline lists each managed table with its pure join tables right after it, and the contribution renders through the provider it runs on")]
    public void The_baseline_lists_the_tables_in_entity_order()
    {
        var schema = OrmSchema.Of("Tok", Plain().Link("Tags", "plain_tags", "plain_id", "tag_id", "tags", "id", null, true), Doc());

        var tables = DdlCompiler.Render(schema.Baseline(), PostgresProvider.Instance).Select(s => s.Split('"')[1]);

        Assert.Equal(new[] { "plains", "plain_tags", "docs" }, tables);
        Assert.Equal(DdlCompiler.Render(schema.Baseline(), PostgresProvider.Instance), schema.Contributions()[0].Render(PostgresProvider.Instance));
    }

    [Fact(DisplayName = "the index-set identity is a hash of the declarations alone, the same whichever providers are loaded, and not the hash of the statements the PostgreSQL provider renders")]
    public void The_index_set_hash_is_structural()
    {
        var schema = OrmSchema.Of("Tok", Doc());
        var rendered = OrmSchema.StableHash(DdlCompiler.Render(schema.Indexes(), PostgresProvider.Instance));

        Assert.Equal(OrmSchema.StableHash(["docs|ix_docs_name|unique|deleted_at|name"]), schema.IndexSetHash);
        Assert.Equal("b590ddf4256c5bca", schema.IndexSetHash);
        Assert.Equal(16, schema.IndexSetHash.Length);
        Assert.NotEqual(rendered, schema.IndexSetHash);
        Assert.NotEqual(schema.IndexSetHash, OrmSchema.Of("Tok", Doc().Index("ix_more", false, false, "name")).IndexSetHash);
        Assert.Equal(schema.IndexSetHash, OrmSchema.Of("Other", Doc()).IndexSetHash);
    }

    [Fact(DisplayName = "the stable hash is the legacy FNV over the ordinally sorted statements, so a reorder of the input changes nothing")]
    public void The_stable_hash_is_the_legacy_formula()
    {
        Assert.Equal("00366bb721fcbbe7", OrmSchema.StableHash([
            "CREATE UNIQUE INDEX IF NOT EXISTS \"ix_live_only_scoped_scope_code\" ON \"live_only_scoped\" (\"scope\", \"code\")",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"ix_live_only_scoped_scope_name_live\" ON \"live_only_scoped\" (\"scope\", \"name\") WHERE \"deleted_at\" IS NULL",
        ]));
        Assert.Equal(OrmSchema.StableHash(["a", "b"]), OrmSchema.StableHash(["b", "a"]));
    }

    [Fact(DisplayName = "a contribution built for a migration type renders its Up through the provider, derives its Down only for a reversible override, and Of renders a plan")]
    public void The_contribution_factories_render_and_declare_down()
    {
        var reversible = MigrationContribution.For<Seed>("v1", Reversibility.Reversible, true);
        var irreversible = MigrationContribution.For<Seed>("v2", Reversibility.Irreversible, true);
        var plain = MigrationContribution.For<Seed>("v3", Reversibility.Reversible, false);
        var plan = MigrationContribution.Of("v4", Reversibility.Reversible, [new DropTable("t")]);

        Assert.Equal(new[] { "DROP TABLE IF EXISTS \"seeds\"" }, reversible.Render(PostgresProvider.Instance));
        var down = new MigrationBuilder(PostgresProvider.Instance);
        reversible.Down!(down);
        Assert.Equal(new[] { "DROP TABLE IF EXISTS \"seeds_down\"" }, down.Render(PostgresProvider.Instance));
        Assert.Null(irreversible.Down);
        Assert.Null(plain.Down);
        Assert.Equal(new[] { "DROP TABLE IF EXISTS \"t\"" }, plan.Render(PostgresProvider.Instance));
    }
}

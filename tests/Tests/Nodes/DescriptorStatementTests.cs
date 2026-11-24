using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.Nodes;

public sealed class DescriptorStatementTests
{
    private static readonly Guid Key = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static EntityDescriptor Note(bool soft = false, bool managed = true)
    {
        var d = EntityDescriptor.Of("global::T.Note", "notes", managed)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("Title", "title", ColumnType.Text, false, false)
            .Property("Body", "body", ColumnType.Json, true, false)
            .Property("Cost", "cost", ColumnType.Decimal, false, false)
            .Property("Rank", "rank", ColumnType.Int32, false, false)
            .Property("At", "at", ColumnType.Timestamp, false, false)
            .Property("Day", "day", ColumnType.Date, false, false)
            .Property("Live", "live", ColumnType.Bool, false, false)
            .Property("Ratio", "ratio", ColumnType.Double, false, false)
            .Property("Big", "big", ColumnType.Int64, false, false)
            .Property("Doc", "doc", ColumnType.Json, false, false)
            .Property("DeletedAt", "deleted_at", ColumnType.Timestamp, true, false);
        return soft ? d.SoftDelete("DeletedAt", "deleted_at", "note") : d;
    }

    private static string Text(Node tree, StatementKind kind = StatementKind.Write) =>
        StatementCompiler.Compile(tree, PostgresProvider.Instance, kind).Text;

    private static StatementParameter[] Parameters(Node tree) =>
        [.. StatementCompiler.Compile(tree, PostgresProvider.Instance, StatementKind.Write).Parameters];

    [Fact(DisplayName = "insert binds one aliased parameter per column in column order and casts a json column")]
    public void Insert_binds_every_column_in_order()
    {
        var values = Enumerable.Range(0, 12).Select(i => (object?)("v" + i)).ToArray();

        var tree = EntityStatements.Insert(Note(), values);

        Assert.Equal(
            "INSERT INTO \"notes\" (\"id\", \"title\", \"body\", \"cost\", \"rank\", \"at\", \"day\", \"live\", \"ratio\", \"big\", \"doc\", \"deleted_at\") "
            + "VALUES (@p0, @p1, @p2::jsonb, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10::jsonb, @p11)",
            Text(tree));
        Assert.Equal(Enumerable.Range(0, 12).Select(i => "p" + i), Parameters(tree).Select(p => p.Name));
        Assert.Equal(values, Parameters(tree).Select(p => p.Value));
    }

    [Fact(DisplayName = "insert refuses a row whose width is not the column count")]
    public void Insert_refuses_a_row_of_the_wrong_width() =>
        Assert.Throws<ArgumentException>(() => EntityStatements.Insert(Note(), [1, 2]));

    [Fact(DisplayName = "get by key is a star select with the key term, and a soft entity adds the live term")]
    public void Get_reads_by_key_and_hides_the_trash()
    {
        Assert.Equal("SELECT * FROM \"notes\" WHERE \"id\" = @k", Text(EntityStatements.Get(Note(), Key), StatementKind.Read));
        Assert.Equal("SELECT * FROM \"notes\" WHERE \"id\" = @k AND \"deleted_at\" IS NULL", Text(EntityStatements.Get(Note(soft: true), Key), StatementKind.Read));
        var parameter = Assert.Single(Parameters(EntityStatements.Get(Note(), Key)));
        Assert.Equal(("k", (object?)Key), (parameter.Name, parameter.Value));
    }

    [Fact(DisplayName = "update sets every non-key column except the soft one, binds s0..sN then the key, and a soft entity adds the live term")]
    public void Update_sets_the_non_key_columns_and_binds_the_key_last()
    {
        var values = Enumerable.Range(0, 12).Select(i => (object?)i).ToArray();

        var tree = EntityStatements.Update(Note(soft: true), values);

        Assert.Equal(
            "UPDATE \"notes\" SET \"title\" = @s0, \"body\" = @s1::jsonb, \"cost\" = @s2, \"rank\" = @s3, \"at\" = @s4, \"day\" = @s5, \"live\" = @s6, \"ratio\" = @s7, \"big\" = @s8, \"doc\" = @s9::jsonb "
            + "WHERE \"id\" = @key AND \"deleted_at\" IS NULL",
            Text(tree));
        var parameters = Parameters(tree);
        Assert.Equal("key", parameters[^1].Name);
        Assert.Equal(0, parameters[^1].Value);
        Assert.Equal(1, parameters[0].Value);
        Assert.Equal(11, parameters.Length);
    }

    [Fact(DisplayName = "an entity with no column outside its key has nothing to update, and one with no key has no statement by key")]
    public void The_refusals_name_the_entity()
    {
        var onlyKey = EntityDescriptor.Of("global::T.Only", "onlys", true).Property("Id", "id", ColumnType.Uuid, false, true);
        var keyless = EntityDescriptor.Of("global::T.Pair", "pairs", true).Property("Tag", "tag", ColumnType.Text, false, false);

        Assert.Contains("global::T.Only", Assert.Throws<InvalidOperationException>(() => EntityStatements.Update(onlyKey, [Key])).Message);
        Assert.Contains("global::T.Pair", Assert.Throws<InvalidOperationException>(() => EntityStatements.Get(keyless, Key)).Message);
        Assert.Contains("global::T.Pair", Assert.Throws<InvalidOperationException>(() => EntityStatements.Delete(keyless, Key)).Message);
    }

    [Theory(DisplayName = "a decimal column takes the declared precision and scale, defaults to 12 and 4, and refuses a shape that is not one")]
    [InlineData(0, 0, 12, 4)]
    [InlineData(19, 4, 19, 4)]
    [InlineData(28, 28, 28, 28)]
    [InlineData(1, 0, 1, 0)]
    public void A_decimal_takes_its_declared_shape(int precision, int scale, int expectedPrecision, int expectedScale)
    {
        var d = EntityDescriptor.Of("global::T.Money", "monies", true).Property("Amount", "amount", ColumnType.Decimal, false, false, precision, scale);

        var column = Assert.Single(d.Columns);
        Assert.Equal((expectedPrecision, expectedScale), (column.Precision, column.Scale));
    }

    [Theory(DisplayName = "a decimal shape beyond what a .NET decimal holds, or with a scale over its precision, is refused by name")]
    [InlineData(29, 4)]
    [InlineData(4, 5)]
    [InlineData(5, -1)]
    [InlineData(0, 2)]
    public void A_decimal_shape_that_is_not_one_is_refused(int precision, int scale)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() =>
            EntityDescriptor.Of("global::T.Money", "monies", true).Property("Amount", "amount", ColumnType.Decimal, false, false, precision, scale));

        Assert.Contains("Amount", refusal.Message, StringComparison.Ordinal);
    }

    // ORML-61 (section 701.1): this test pinned that a composite key silently filtered on the FIRST key, so an UPDATE
    // or DELETE by key matched every row sharing it. That is the defect, so the assertion is replaced by the refusal.
    [Fact(DisplayName = "a descriptor with more than one key flag is refused by every statement by key, "
        + "naming the entity, instead of filtering on the first key")]
    public void A_composite_key_is_refused_by_every_statement_by_key()
    {
        var d = EntityDescriptor.Of("global::T.Link", "links", true)
            .Property("A", "a", ColumnType.Uuid, false, true)
            .Property("B", "b", ColumnType.Uuid, false, true)
            .Property("Note", "note", ColumnType.Text, false, false);

        var update = Assert.Throws<InvalidOperationException>(() => EntityStatements.Update(d, [Key, Key, "n"]));
        var delete = Assert.Throws<InvalidOperationException>(() => EntityStatements.Delete(d, Key));
        var get = Assert.Throws<InvalidOperationException>(() => EntityStatements.Get(d, Key));

        foreach (var refusal in new[] { update, delete, get })
            Assert.Contains("T.Link has more than one [Key] property", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the public by-key read of OrmExec refuses a composite key before it touches the connection, "
        + "and a descriptor with no key keeps its own refusal")]
    public async Task OrmExec_refuses_a_composite_key_and_keeps_the_no_key_refusal()
    {
        var composite = EntityDescriptor.Of("global::T.Link", "links", true)
            .Property("A", "a", ColumnType.Uuid, false, true)
            .Property("B", "b", ColumnType.Uuid, false, true);
        var keyless = EntityDescriptor.Of("global::T.Bare", "bares", true)
            .Property("Note", "note", ColumnType.Text, false, false);
        var db = new OrmContext(new NeverUsedDataSource(), PostgresProvider.Instance);

        var many = await Assert.ThrowsAsync<InvalidOperationException>(
            () => OrmExec.GetAsync(db, null!, composite, Key, _ => new object()));
        var none = await Assert.ThrowsAsync<InvalidOperationException>(
            () => OrmExec.GetAsync(db, null!, keyless, Key, _ => new object()));

        Assert.Contains("more than one [Key] property", many.Message, StringComparison.Ordinal);
        Assert.Contains("has no [Key] property", none.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "delete by key is physical for a plain entity and the soft-delete update for a soft one")]
    public void Delete_is_physical_or_soft()
    {
        Assert.Equal("DELETE FROM \"notes\" WHERE \"id\" = @k", Text(EntityStatements.Delete(Note(), Key)));
        Assert.Equal("UPDATE \"notes\" SET \"deleted_at\" = now() WHERE \"id\" = @k AND \"deleted_at\" IS NULL", Text(EntityStatements.Delete(Note(soft: true), Key)));
    }

    private static ManyToManyDescriptor Link(string? soft = null, bool pure = true) =>
        new("Tags", "note_tags", "note_id", "tag_id", "tags", "id", soft, pure);

    [Fact(DisplayName = "the many-to-many load joins the join table, filters on this side and hides trashed rows of the other side")]
    public void The_link_load_joins_the_join_table()
    {
        Assert.Equal(
            "SELECT o.* FROM \"tags\" o JOIN \"note_tags\" j ON j.\"tag_id\" = o.\"id\" WHERE j.\"note_id\" = @k",
            Text(EntityStatements.LoadLinked(Note(), Link(), Key), StatementKind.Read));
        Assert.Equal(
            "SELECT o.* FROM \"tags\" o JOIN \"note_tags\" j ON j.\"tag_id\" = o.\"id\" WHERE j.\"note_id\" = @k AND o.\"deleted_at\" IS NULL",
            Text(EntityStatements.LoadLinked(Note(), Link("deleted_at"), Key), StatementKind.Read));
    }

    [Fact(DisplayName = "adding a link ignores a pair that exists and removing one deletes exactly that pair, each with the legacy parameter names")]
    public void The_link_writes_use_a_and_b()
    {
        var add = EntityStatements.AddLink(Link(), Key, Key);
        var remove = EntityStatements.RemoveLink(Link(), Key, Key);

        Assert.Equal("INSERT INTO \"note_tags\" (\"note_id\", \"tag_id\") VALUES (@a, @b) ON CONFLICT DO NOTHING", Text(add));
        Assert.Equal("DELETE FROM \"note_tags\" WHERE \"note_id\" = @a AND \"tag_id\" = @b", Text(remove));
        Assert.Equal(new[] { "a", "b" }, Parameters(add).Select(p => p.Name));
        Assert.Equal(new[] { "a", "b" }, Parameters(remove).Select(p => p.Name));
    }

    [Fact(DisplayName = "the baseline holds one table per managed entity with the column defaults of its types, and none for an unmanaged one")]
    public void The_create_table_carries_the_defaults()
    {
        var table = Assert.Single(EntityStatements.CreateTables(Note()));

        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS \"notes\" (\"id\" uuid NOT NULL PRIMARY KEY, \"title\" text NOT NULL DEFAULT '', \"body\" jsonb, \"cost\" numeric(12, 4) NOT NULL, "
            + "\"rank\" integer NOT NULL DEFAULT 0, \"at\" timestamp with time zone NOT NULL DEFAULT now(), \"day\" date NOT NULL, \"live\" boolean NOT NULL DEFAULT false, "
            + "\"ratio\" double precision NOT NULL DEFAULT 0, \"big\" bigint NOT NULL DEFAULT 0, \"doc\" jsonb NOT NULL DEFAULT '{}'::jsonb, \"deleted_at\" timestamp with time zone)",
            DdlCompiler.Render([table], PostgresProvider.Instance).Single());
        Assert.Empty(EntityStatements.CreateTables(Note(managed: false)));
        Assert.Empty(EntityStatements.CreateJoinTables(Note(managed: false).Link("Tags", "note_tags", "note_id", "tag_id", "tags", "id", null, true)));
    }

    [Fact(DisplayName = "a pure join table is created with its two uuid columns and a composite key; a mapped one is not created")]
    public void Join_tables_are_created_only_when_pure()
    {
        var d = Note().Link("Tags", "note_tags", "note_id", "tag_id", "tags", "id", null, true).Link("Mapped", "note_pins", "note_id", "pin_id", "pins", "id", null, false);

        var join = Assert.Single(EntityStatements.CreateJoinTables(d));

        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS \"note_tags\" (\"note_id\" uuid NOT NULL, \"tag_id\" uuid NOT NULL, PRIMARY KEY (\"note_id\", \"tag_id\"))",
            DdlCompiler.Render([join], PostgresProvider.Instance).Single());
    }

    [Fact(DisplayName = "an index is partial over the live rows only when it is LiveOnly on a soft entity, and unmanaged entities create none")]
    public void Indexes_follow_the_soft_declaration()
    {
        var plain = Note().Index("ix_a", true, true, "title");
        var soft = Note(soft: true).Index("ix_a", true, true, "title").Index("ix_b", false, false, "rank", "title");

        Assert.Equal("CREATE UNIQUE INDEX IF NOT EXISTS \"ix_a\" ON \"notes\" (\"title\")", DdlCompiler.Render(EntityStatements.CreateIndexes(plain), PostgresProvider.Instance).Single());
        Assert.Equal(
            new[]
            {
                "CREATE UNIQUE INDEX IF NOT EXISTS \"ix_a\" ON \"notes\" (\"title\") WHERE \"deleted_at\" IS NULL",
                "CREATE INDEX IF NOT EXISTS \"ix_b\" ON \"notes\" (\"rank\", \"title\")",
            },
            DdlCompiler.Render(EntityStatements.CreateIndexes(soft), PostgresProvider.Instance));
        Assert.Empty(EntityStatements.CreateIndexes(Note(managed: false).Index("ix_a", true, false, "title")));
    }

    [Fact(DisplayName = "the reconcile item of a LiveOnly index guards the key columns plus the soft column once and carries the collision keys; a plain index carries none")]
    public void Reconcile_items_guard_the_columns()
    {
        var soft = Note(soft: true).Index("ix_live", true, true, "title").Index("ix_live_soft", true, true, "title", "deleted_at").Index("ix_plain", false, false, "rank");

        var items = EntityStatements.ReconcileIndexes(soft);

        Assert.Equal(new[] { "title", "deleted_at" }, items[0].GuardColumns.Cast<ColumnRef>().Select(c => c.Column));
        Assert.Equal(new[] { "title" }, items[0].CollisionKeys!.Cast<ColumnRef>().Select(c => c.Column));
        Assert.Equal(new[] { "title", "deleted_at" }, items[1].GuardColumns.Cast<ColumnRef>().Select(c => c.Column));
        Assert.Equal(new[] { "rank" }, items[2].GuardColumns.Cast<ColumnRef>().Select(c => c.Column));
        Assert.Null(items[2].CollisionKeys);
        Assert.Empty(EntityStatements.ReconcileIndexes(Note(managed: false).Index("ix", false, false, "rank")));
    }

    [Fact(DisplayName = "the descriptor answers the column of a property and refuses an unmapped one with the entity name")]
    public void The_column_of_a_property_is_answered_or_refused()
    {
        Assert.Equal("title", Note().ColumnOf("Title"));
        var refusal = Assert.Throws<InvalidOperationException>(() => Note().ColumnOf("Missing"));
        Assert.StartsWith("global::T.Note has no COLUMN for the property `Missing`.", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the soft metadata a descriptor yields carries the declared tenant, label, owners, pinned and status columns, and the live-only sets")]
    public void The_soft_metadata_is_derived_from_the_descriptor()
    {
        var d = Note(soft: true).Tenant("rank").Label("title").Owners("rank").Pinned("title").StatusColumns("big").Unversioned("at").Index("ix", true, true, "title");

        var meta = d.ToSoftMeta()!;

        Assert.Equal(("DeletedAt", false, "rank", "id", "title", "note"), (meta.Column, meta.Platform, meta.TenantColumn, meta.KeyColumn, meta.LabelColumn, meta.EntityToken));
        Assert.Equal(new[] { "rank" }, meta.Owners);
        Assert.Equal(new[] { "title" }, meta.PinnedColumns);
        Assert.Equal(new[] { "big" }, meta.StatusColumns);
        Assert.Equal(new[] { "at" }, meta.Unversioned);
        Assert.Equal(new[] { new[] { "title" } }, meta.LiveOnlyIndexes);
        Assert.Equal(12, meta.PropertyColumns!.Length);
        Assert.Null(Note().ToSoftMeta());
    }

    [Fact(DisplayName = "a history table needs a soft entity with a sole key, and a platform entity carries an empty tenant column")]
    public void The_history_table_needs_a_soft_entity_and_a_sole_key()
    {
        var keyless = EntityDescriptor.Of("global::T.Pair", "pairs", true).Property("Tag", "tag", ColumnType.Text, false, false).SoftDelete("Tag", "tag", "pair");
        var composite = Note(soft: true).Property("Other", "other", ColumnType.Uuid, false, true);

        var platform = Note(soft: true).HistoryTable()!;
        var tenant = Note(soft: true).Tenant("rank").Unversioned("at").HistoryTable()!;

        Assert.Null(Note().HistoryTable());
        Assert.Null(keyless.HistoryTable());
        Assert.Null(composite.HistoryTable());
        Assert.Equal(("notes", "id", "deleted_at", ""), (platform.Table, platform.KeyColumn, platform.SoftColumn, platform.TenantColumn));
        Assert.Equal(("notes", "id", "deleted_at", "rank"), (tenant.Table, tenant.KeyColumn, tenant.SoftColumn, tenant.TenantColumn));
        Assert.Equal(new[] { "at" }, tenant.Unversioned);
    }
}

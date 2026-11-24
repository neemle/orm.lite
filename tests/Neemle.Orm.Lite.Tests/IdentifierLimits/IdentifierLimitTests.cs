using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.IdentifierLimits;

// ORML-29 (B42): a name a migration creates is checked against the identifier limit of the provider,
// which PostgreSQL would otherwise cut at 63 bytes without a word.
public sealed class IdentifierLimitTests
{
    private static readonly ColumnRef A = new("a");

    private static readonly ColumnDef Text = new("a", new TypeRef(ColumnType.Text));

    private static string Bytes(int count) => new('n', count);

    private static ReconcileIndexes Reconcile(string index) =>
        new([new ReconcileIndex(new CreateIndex(index, "docs", [A], true), [A])]);

    private static IdentifierTooLongException Refused(Node operation) =>
        Assert.Throws<IdentifierTooLongException>(() => DdlCompiler.Compile(operation, PostgresProvider.Instance));

    private static IReadOnlyList<Statement> Compiled(Node operation) =>
        DdlCompiler.Compile(operation, PostgresProvider.Instance);

    [Fact(DisplayName = "the PostgreSQL provider states 63 bytes as its identifier limit and prints it in the "
        + "manifest, and the SQLite provider states none")]
    public void The_limit_is_a_capability_of_the_provider()
    {
        var postgres = PostgresProvider.Instance.Capabilities;
        var sqlite = SqliteProvider.Instance.Capabilities;

        Assert.Equal(63, postgres.MaxIdentifierBytes);
        Assert.Equal("MaxIdentifierBytes=63", postgres.Manifest()[^1]);
        Assert.Null(sqlite.MaxIdentifierBytes);
        Assert.DoesNotContain(sqlite.Manifest(), l => l.StartsWith("MaxIdentifierBytes", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "changing one capability level keeps the identifier limit, and a limit below one byte "
        + "is refused")]
    public void The_limit_survives_a_level_change_and_must_be_positive()
    {
        var set = CapabilitySet.Everything().WithMaxIdentifierBytes(10).With(Capability.Upsert, Support.None);

        Assert.Equal(10, set.MaxIdentifierBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => CapabilitySet.Everything().WithMaxIdentifierBytes(0));
    }

    [Fact(DisplayName = "an index named with 63 bytes compiles on PostgreSQL and one byte more is refused by "
        + "name, kind, length and limit")]
    public void An_index_name_is_refused_one_byte_past_the_limit()
    {
        var kept = Compiled(new CreateIndex(Bytes(63), "docs", [A]));

        var refusal = Refused(new CreateIndex(Bytes(64), "docs", [A]));

        Assert.Contains(Bytes(63), Assert.Single(kept).Text, StringComparison.Ordinal);
        Assert.Equal(
            ("postgres", "index", Bytes(64), 64, 63),
            (refusal.Provider, refusal.Kind, refusal.Name, refusal.Bytes, refusal.Limit));
        Assert.Contains("64 bytes", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("at most 63", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the limit counts UTF-8 bytes: 32 two-byte letters are refused and 31 of them with "
        + "one more letter are kept")]
    public void The_limit_counts_bytes_not_characters()
    {
        var wide = new string('é', 32);
        var fits = new string('é', 31) + "n";

        Assert.Equal(64, System.Text.Encoding.UTF8.GetByteCount(wide));
        Assert.Equal(wide, Refused(new CreateIndex(wide, "docs", [A])).Name);
        Assert.Single(Compiled(new CreateIndex(fits, "docs", [A])));
    }

    [Fact(DisplayName = "two index names that agree in their first 63 bytes are both refused, so the second "
        + "can no longer be skipped as already existing")]
    public void Names_that_would_truncate_to_one_are_refused()
    {
        var prefix = Bytes(63);

        Assert.Equal(prefix + "_a", Refused(new CreateIndex(prefix + "_a", "docs", [A])).Name);
        Assert.Equal(prefix + "_live", Refused(new CreateIndex(prefix + "_live", "docs", [A], true)).Name);
    }

    [Fact(DisplayName = "a reconcile set with a long index name is refused by name before any statement is built")]
    public void A_reconciled_index_with_a_long_name_is_refused()
    {
        var refusal = Assert.Throws<IdentifierTooLongException>(
            () => DdlCompiler.Validate(Reconcile(Bytes(70)), PostgresProvider.Instance));

        Assert.Equal(("index", Bytes(70)), (refusal.Kind, refusal.Name));
        DdlCompiler.Validate(Reconcile(Bytes(63)), PostgresProvider.Instance);
    }

    [Fact(DisplayName = "a table, a column, a renamed column, a foreign key constraint and a new column with "
        + "a long name are each refused with their kind")]
    public void Every_declared_kind_of_name_is_checked()
    {
        var long64 = Bytes(64);
        var column = new ColumnDef(long64, new TypeRef(ColumnType.Text));
        var foreignKey = new ForeignKeyDef(long64, [A], "other", [new ColumnRef("id")]);
        (Node Operation, string Kind)[] cases =
        [
            (new CreateTable(long64, [Text]), "table"),
            (new CreateTable("docs", [column]), "column"),
            (new CreateTable("docs", [Text], [foreignKey]), "constraint"),
            (new AddColumn("docs", column), "column"),
            (new RenameColumn("docs", "a", long64), "column"),
            (new AddForeignKey(long64, "docs", [A], "other", [new ColumnRef("id")]), "constraint"),
        ];

        foreach (var (operation, kind) in cases)
        {
            var refusal = Refused(operation);
            Assert.Equal((kind, long64), (refusal.Kind, refusal.Name));
        }
    }

    [Fact(DisplayName = "dropping, or renaming from, a name that is already long is not refused, because the "
        + "server cut it the same way when it was made")]
    public void Names_that_are_only_looked_up_are_not_checked()
    {
        var long64 = Bytes(64);

        Assert.Single(Compiled(new DropIndex(long64)));
        Assert.Single(Compiled(new DropTable(long64)));
        Assert.Single(Compiled(new DropForeignKey(long64, "docs")));
        Assert.Single(Compiled(new RenameColumn("docs", long64, "b")));
    }

    [Fact(DisplayName = "a provider that states no identifier limit refuses no length: SQLite keeps a "
        + "200-byte index name")]
    public void A_provider_without_a_limit_checks_nothing()
    {
        var wide = new ColumnDef(Bytes(200), new TypeRef(ColumnType.Text));

        DdlCompiler.Validate(new CreateIndex(Bytes(200), "docs", [A]), SqliteProvider.Instance);
        DdlCompiler.Validate(new CreateTable(Bytes(200), [wide]), SqliteProvider.Instance);
    }
}

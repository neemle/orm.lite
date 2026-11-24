using System.Reflection;
using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Nodes;

public sealed class DdlLoweringTests
{
    private static readonly string[] One = ["owner_id"];

    private static readonly string[] RefOne = ["id"];

    private static readonly string[] Plain = ["name"];

    private static readonly string[] Lowered = ["lower(name)", "scope"];

    private static Node Lower(Node op) => PostgresProvider.Instance.Lower(op);

    private static string Text(Node op) => DdlText.Of(op);

    private static PgBlock Block(Node op) => Assert.IsType<PgBlock>(Lower(op));

    private static Node FkUp(string newAction = ForeignKeyActionDdl.Cascade, string oldAction = ForeignKeyActionDdl.NoAction, string onUpdate = ForeignKeyActionDdl.NoAction) =>
        ForeignKeyActionDdl.Up("samples", "fk_samples_owner", One, "owners", RefOne, newAction, oldAction, onUpdate);

    [Fact(DisplayName = "a foreign key conversion lowers to a guarded block: set the lock timeout, read the current action, and replace the constraint only when the action differs")]
    public void A_foreign_key_conversion_is_a_guarded_block()
    {
        var block = Block(FkUp());

        Assert.Equal("fk", block.Tag);
        Assert.Equal(new PgDeclare("current_action", "text"), Assert.Single(block.Declarations));
        Assert.IsType<PgPerform>(block.Statements[0]);
        var lookup = Assert.IsType<PgQuery>(block.Statements[1]);
        Assert.Equal("current_action", lookup.Into);
        var guard = Assert.IsType<PgIf>(block.Statements[2]);
        var replace = Assert.IsType<PgReplaceConstraint>(Assert.Single(guard.Then));
        Assert.Equal(ReferentialAction.Cascade, replace.Added.OnDelete);
        Assert.Equal(ReferentialAction.NoAction, replace.Added.OnUpdate);
        Assert.Empty(guard.Otherwise);
        var condition = Assert.IsType<PgAll>(guard.Condition);
        Assert.Equal(new IsNotNull(new PgName("current_action")), condition.Terms[0]);
        Assert.Equal(new Compare(CompareOp.NotEqual, new PgName("current_action"), new StringLit("c")), condition.Terms[1]);
    }

    [Theory(DisplayName = "the converted action is the one written after ON DELETE and the one the guard compares the catalog code against")]
    [InlineData(ForeignKeyActionDdl.Cascade, "c", "ON DELETE CASCADE")]
    [InlineData(ForeignKeyActionDdl.SetNull, "n", "ON DELETE SET NULL")]
    public void The_converted_action_is_written_and_compared(string action, string code, string clause)
    {
        var text = Text(FkUp(action));

        Assert.Contains(clause + "; END IF; END $fk$", text, StringComparison.Ordinal);
        Assert.Contains("current_action <> '" + code + "' THEN", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the Down of a foreign key conversion writes the restored action and compares against its code, and the on-update action is kept")]
    public void The_down_restores_the_old_action()
    {
        var down = Text(ForeignKeyActionDdl.Down("samples", "fk", One, "owners", RefOne, ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.Restrict, ForeignKeyActionDdl.Restrict));

        Assert.Contains("ON UPDATE RESTRICT ON DELETE RESTRICT;", down, StringComparison.Ordinal);
        Assert.Contains("current_action <> 'r' THEN", down, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the catalog code of each referential action is the pg_constraint letter, and an unknown action is refused")]
    public void Catalog_codes_are_the_pg_constraint_letters()
    {
        Assert.Equal("a", PostgresCatalog.ForeignKeyCode(ReferentialAction.NoAction));
        Assert.Equal("r", PostgresCatalog.ForeignKeyCode(ReferentialAction.Restrict));
        Assert.Equal("c", PostgresCatalog.ForeignKeyCode(ReferentialAction.Cascade));
        Assert.Equal("n", PostgresCatalog.ForeignKeyCode(ReferentialAction.SetNull));
        Assert.Throws<ArgumentException>(() => PostgresCatalog.ForeignKeyCode((ReferentialAction)99));
    }

    [Fact(DisplayName = "the live-only Up creates the live unique index with its soft-column predicate on both branches, marks it with the version and drops the total index or its owning constraint")]
    public void The_live_only_up_creates_the_partial_unique_index()
    {
        var up = LiveOnlyConversionDdl.Up("samples", Lowered, "deleted_at", "uq_name", "ix_live", false, "0200_live_only");
        var block = Block(up);

        Assert.Equal("lc", block.Tag);
        var present = Assert.IsType<PgIf>(block.Statements[4]);
        var created = Assert.IsType<CreateIndex>(present.Then[0]);
        Assert.True(created.Unique);
        Assert.Equal(new Where([new IsNull(new ColumnRef("deleted_at"))]), created.Where);
        Assert.Equal(created, Assert.Single(present.Otherwise));
        var text = Text(up);
        const string create = "CREATE UNIQUE INDEX IF NOT EXISTS \"ix_live\" ON \"samples\" (lower(\"name\"), \"scope\") WHERE \"deleted_at\" IS NULL;";
        Assert.Equal(2, text.Split(create).Length - 1);
        Assert.Contains("COMMENT ON INDEX \"ix_live\" IS '0200_live_only';", text, StringComparison.Ordinal);
        Assert.Contains(
            "IF owner_constraint IS NULL THEN EXECUTE 'DROP INDEX ' || total_index::regclass; ELSE EXECUTE "
            + "format('ALTER TABLE \"samples\" DROP CONSTRAINT %I', owner_constraint); END IF;",
            text,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the live-only Down refuses with a named error when a duplicate key exists before it recreates the total index, then releases the live index by its declaration")]
    public void The_live_only_down_refuses_duplicates_before_recreating()
    {
        var declared = Text(LiveOnlyConversionDdl.Down("samples", Plain, "deleted_at", "uq_name", "ix_live", true, "v1"));
        var undeclared = Text(LiveOnlyConversionDdl.Down("samples", Plain, "deleted_at", "uq_name", "ix_live", false, "v1"));

        const string refusal = "RAISE EXCEPTION 'cannot recreate unique index uq_name on samples (name): duplicate key % exists, nothing was deleted', quote_literal(duplicate_key); END IF;";
        Assert.Contains(refusal, declared, StringComparison.Ordinal);
        Assert.True(declared.IndexOf(refusal, StringComparison.Ordinal) < declared.IndexOf("CREATE UNIQUE INDEX IF NOT EXISTS \"uq_name\"", StringComparison.Ordinal));
        Assert.EndsWith(
            "EXECUTE format('COMMENT ON INDEX %s IS NULL', live_index::regclass); END IF; END $lc$",
            declared,
            StringComparison.Ordinal);
        Assert.EndsWith(
            "EXECUTE 'DROP INDEX ' || live_index::regclass; END IF; END $lc$", undeclared, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the soft delete column Up adds the column only when it is missing and marks it, and the Down drops it only when the marker matches and no soft-deleted row would turn live")]
    public void The_soft_column_blocks_are_guarded_by_the_catalog()
    {
        var up = Text(SoftDeleteColumnDdl.Up("samples", "0300_soft"));
        var down = Text(SoftDeleteColumnDdl.Down("samples", "0300_soft"));

        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = to_regclass('\"samples\"')", up, StringComparison.Ordinal);
        Assert.Contains("ADD COLUMN \"deleted_at\" timestamp with time zone NULL; COMMENT ON COLUMN \"samples\".\"deleted_at\" IS '0300_soft';", up, StringComparison.Ordinal);
        Assert.Contains("= '0300_soft' THEN SELECT count(*) INTO soft_rows", down, StringComparison.Ordinal);
        Assert.Contains("IF soft_rows > 0 THEN RAISE EXCEPTION", down, StringComparison.Ordinal);
        Assert.EndsWith("ALTER TABLE \"samples\" DROP COLUMN \"deleted_at\"; END IF; END $sd$", down, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "every block starts by setting the lock timeout, so no conversion waits on a lock past five seconds")]
    public void Every_block_sets_the_lock_timeout_first()
    {
        Node[] ops =
        [
            FkUp(),
            LiveOnlyConversionDdl.Up("samples", Plain, "deleted_at", "uq", "ix", true, "v1"),
            LiveOnlyConversionDdl.Down("samples", Plain, "deleted_at", "uq", "ix", true, "v1"),
            SoftDeleteColumnDdl.Up("samples", "v1"),
            SoftDeleteColumnDdl.Down("samples", "v1"),
        ];

        foreach (var op in ops)
            Assert.Contains("BEGIN PERFORM set_config('lock_timeout', '5s', true);", Text(op), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the row history install lowers to a script of one statement per piece in a fixed order, then one attach block per table")]
    public void The_row_history_install_is_a_script()
    {
        var tables = new[] { new RowHistoryTable("a_tab", "id", "deleted_at", "tenant_id", ["updated_at"]), new RowHistoryTable("b_tab", "id", "deleted_at", "", []) };
        var script = Assert.IsType<Script>(Lower(RowHistoryDdl.Install(tables)));

        Assert.IsType<PgSetLocal>(script.Statements[0]);
        Assert.Equal(PostgresDdlDataLiterals_RowHistoryTable, Assert.IsType<PgDataLiteral>(script.Statements[1]).Text);
        for (var i = 2; i <= 4; i++) Assert.Equal("rh", Assert.IsType<PgBlock>(script.Statements[i]).Tag);
        Assert.StartsWith("CREATE OR REPLACE FUNCTION row_history_capture()", Assert.IsType<PgDataLiteral>(script.Statements[5]).Text, StringComparison.Ordinal);
        Assert.Equal(8, script.Statements.Count);
        var texts = DdlText.Statements(RowHistoryDdl.Install(tables)).Select(s => s.Text).ToList();
        Assert.Contains("to_regclass('\"a_tab\"')", texts[6], StringComparison.Ordinal);
        Assert.Contains("to_regclass('\"b_tab\"')", texts[7], StringComparison.Ordinal);
        Assert.All(DdlText.Statements(RowHistoryDdl.Install(tables)), s => Assert.Equal(StatementKind.Ddl, s.Kind));
    }

    private const string PostgresDdlDataLiterals_RowHistoryTable =
        "CREATE TABLE IF NOT EXISTS row_history ( id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, tenant_id uuid NULL, "
        + "source_table text NOT NULL, row_id uuid NOT NULL, op text NOT NULL CHECK (op IN ('update', 'soft_delete', 'restore')), "
        + "old_values jsonb NOT NULL, actor_user_id uuid NULL, actor_kind text NOT NULL CHECK (actor_kind IN ('user', 'system', 'unknown')), "
        + "xact_id bigint NOT NULL, changed_at timestamptz NOT NULL DEFAULT now())";

    [Fact(DisplayName = "the capture function carries the actor setting and refuses a setting that could close its string literal")]
    public void The_capture_function_carries_the_actor_setting()
    {
        var text = Text(new RowHistoryFunction("app.actor"));

        Assert.Contains("v_actor := current_setting('app.actor', true);", text, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => Text(new RowHistoryFunction("a'b")));
        Assert.Throws<ArgumentException>(() => Text(new RowHistoryFunction("A.b")));
    }

    [Fact(DisplayName = "a compiled DDL statement carries the Ddl kind and no parameters, and a session lock carries its named key")]
    public void Statement_kinds_and_parameters()
    {
        var ddl = Assert.Single(DdlCompiler.Compile(new DropTable("t"), PostgresProvider.Instance));
        var lease = StatementCompiler.Compile(AdvisorySessionLocks.TryLock(727274), PostgresProvider.Instance, StatementKind.Read);

        Assert.Equal(StatementKind.Ddl, ddl.Kind);
        Assert.Empty(ddl.Parameters);
        Assert.Equal("SELECT pg_try_advisory_lock(@key)", lease.Text);
        Assert.Equal(new StatementParameter(0, "key", 727274L), Assert.Single(lease.Parameters));
        Assert.Equal("SELECT pg_advisory_unlock(@key)", StatementCompiler.Compile(AdvisorySessionLocks.Unlock(1), PostgresProvider.Instance, StatementKind.Read).Text);
        Assert.Equal("SELECT pg_advisory_lock(727274)", Assert.Single(new PostgresMigrationPlanner(PostgresProvider.Instance).PlanRun(PostgresProvider.Instance).Acquire).Text);
    }

    [Fact(DisplayName = "the named-constants class of the DDL text holds nothing but constants, and the history names it repeats equal the core names")]
    public void The_ddl_constants_class_holds_only_constants()
    {
        var root = RuntimeSources.LibraryRoot();
        var file = Path.Combine(root, "src", "Neemle.Orm.Lite.Postgres", "PostgresDdlDataLiterals.cs");
        var text = File.ReadAllText(file);

        Assert.Contains("internal static class PostgresDdlDataLiterals", text, StringComparison.Ordinal);
        Assert.DoesNotContain("static void", text, StringComparison.Ordinal);
        Assert.DoesNotContain("=>", text, StringComparison.Ordinal);

        var literals = typeof(PostgresProvider).Assembly.GetType("Neemle.Orm.Lite.Postgres.PostgresDdlDataLiterals")!;
        string Const(string name) => (string)literals.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
        Assert.Equal(RowHistoryDdl.TableName, Const("RowHistoryTableName"));
        Assert.Equal(RowHistoryDdl.FunctionName, Const("RowHistoryFunctionName"));
        Assert.Equal(RowHistoryDdl.UpdateTrigger, Const("RowHistoryUpdateTrigger"));
        Assert.Equal(RowHistoryDdl.DeleteTrigger, Const("RowHistoryDeleteTrigger"));
    }

    [Fact(DisplayName = "a conversion node the provider does not lower is refused by the standard writer with the node's name")]
    public void An_unlowered_conversion_is_refused()
    {
        var serializer = new PostgresSerializer();

        var error = Assert.Throws<NotSupportedException>(() => serializer.Serialize(SoftDeleteColumnDdl.Up("t", "v1")));

        Assert.Contains(nameof(SoftDeleteColumn), error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a provider node is written only by the PostgreSQL serializer")]
    public void A_provider_node_needs_its_own_serializer()
    {
        var other = new Neemle.Orm.Lite.Tests.FixtureSerializer();

        Assert.Throws<NotSupportedException>(() => other.Serialize(new PgName("x")));
    }
}

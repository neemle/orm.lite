using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Nodes;

public sealed class MigrationReversibilityTests
{
    private static readonly string[] One = ["owner_id"];

    private static readonly string[] RefOne = ["id"];

    private sealed class CreatesTableAndIndex : Migration
    {
        public override void Up(MigrationBuilder b) => b
            .CreateTable("samples", t => t.Column("id", ColumnType.Uuid, false, true).Column("name", ColumnType.Text))
            .AddColumn("samples", "note", ColumnType.Text)
            .RenameColumn("samples", "note", "remark")
            .CreateIndex("ix_samples_name", "samples", "name")
            .AddForeignKey("fk_samples_owner", "samples", "owner_id", "owners", "id");
    }

    private sealed class DropsAColumn : Migration
    {
        public override void Up(MigrationBuilder b) => b.CreateIndex("ix", "t", "a").DropColumn("t", "a");
    }

    private sealed class RunsRawSql : Migration
    {
        public override void Up(MigrationBuilder b) => b.CreateIndex("ix", "t", "a").Raw("postgres", "SELECT 1");
    }

    private sealed class ExplicitDown : Migration
    {
        public override void Up(MigrationBuilder b) => b.DropColumn("t", "a");

        public override void Down(MigrationBuilder b) => b.AddColumn("t", "a", ColumnType.Text);
    }

    private static IReadOnlyList<Node> Up(Migration m)
    {
        var b = new MigrationBuilder(PostgresProvider.Instance);
        m.Up(b);
        return b.Nodes;
    }

    private static IReadOnlyList<string> DownText(Migration m)
    {
        var b = new MigrationBuilder(PostgresProvider.Instance);
        m.Down(b);
        return b.Operations;
    }

    [Fact(DisplayName = "each operation node has one class: creates, additions, renames and conversions are Reversible, drops and deletes are Lossy, and raw SQL, inserts, history and locks are Unknown")]
    public void Every_operation_has_its_class()
    {
        var column = new ColumnRef("c");
        var def = new ColumnDef("c", new TypeRef(ColumnType.Text));
        var expected = new (Node Op, OpClass Class)[]
        {
            (new CreateTable("t", [def]), OpClass.Reversible),
            (new AddColumn("t", def), OpClass.Reversible),
            (new CreateIndex("i", "t", [column]), OpClass.Reversible),
            (new RenameColumn("t", "a", "b"), OpClass.Reversible),
            (new AddForeignKey("fk", "t", [column], "o", [column]), OpClass.Reversible),
            (ForeignKeyActionDdl.Up("t", "fk", One, "o", RefOne, ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.NoAction), OpClass.Reversible),
            (LiveOnlyConversionDdl.Up("t", ["a"], "deleted_at", "uq", "ix", true, "v1"), OpClass.Reversible),
            (SoftDeleteColumnDdl.Up("t", "v1"), OpClass.Reversible),
            (new DropTable("t"), OpClass.Lossy),
            (new DropColumn("t", "c"), OpClass.Lossy),
            (new DropIndex("i"), OpClass.Lossy),
            (new DropForeignKey("fk", "t"), OpClass.Lossy),
            (new DeleteData("t", "c", [new IntLit(1)]), OpClass.Lossy),
            (new RawSql(null, "x"), OpClass.Unknown),
            (new InsertData("t", [column], [new DataRow([new IntLit(1)])]), OpClass.Unknown),
            (new RowHistoryInstall("a.b", []), OpClass.Unknown),
            (new SessionLock(SessionLockOp.Wait, new IntLit(1)), OpClass.Unknown),
        };

        foreach (var (op, expectedClass) in expected)
        {
            Assert.Equal(expectedClass, MigrationOps.ClassOf(op));
            Assert.Equal(expectedClass == OpClass.Reversible, MigrationOps.Inverse(op) is not null);
        }
    }

    [Fact(DisplayName = "an inverse undoes its operation: a create becomes a drop, an addition a removal, a rename swaps its names, a conversion swaps its direction, and the inverse of an inverse is the original")]
    public void Inverses_undo_their_operation()
    {
        var def = new ColumnDef("c", new TypeRef(ColumnType.Text));
        var fk = ForeignKeyActionDdl.Up("t", "fk", One, "o", RefOne, ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.Restrict);
        var live = LiveOnlyConversionDdl.Up("t", ["a"], "deleted_at", "uq", "ix", true, "v1");
        var soft = SoftDeleteColumnDdl.Up("t", "v1");

        Assert.Equal(new DropTable("t"), MigrationOps.Inverse(new CreateTable("t", [def])));
        Assert.Equal(new DropColumn("t", "c"), MigrationOps.Inverse(new AddColumn("t", def)));
        Assert.Equal(new DropIndex("i"), MigrationOps.Inverse(new CreateIndex("i", "t", [new ColumnRef("c")])));
        Assert.Equal(new RenameColumn("t", "b", "a"), MigrationOps.Inverse(new RenameColumn("t", "a", "b")));
        Assert.Equal(new DropForeignKey("fk", "t"), MigrationOps.Inverse(new AddForeignKey("fk", "t", [new ColumnRef("c")], "o", [new ColumnRef("c")])));
        Assert.Equal(ForeignKeyActionDdl.Down("t", "fk", One, "o", RefOne, ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.Restrict), MigrationOps.Inverse(fk));
        Assert.Equal(LiveOnlyConversionDdl.Down("t", ["a"], "deleted_at", "uq", "ix", true, "v1"), MigrationOps.Inverse(live));
        Assert.Equal(SoftDeleteColumnDdl.Down("t", "v1"), MigrationOps.Inverse(soft));
        foreach (var op in new[] { fk, live, soft })
            Assert.Equal(op, MigrationOps.Inverse(MigrationOps.Inverse(op)!));
    }

    [Fact(DisplayName = "a Down derived from Up runs the inverses in reverse order")]
    public void The_derived_down_runs_in_reverse_order()
    {
        var down = DownText(new CreatesTableAndIndex());

        Assert.Equal(
            [
                "ALTER TABLE \"samples\" DROP CONSTRAINT \"fk_samples_owner\"",
                "DROP INDEX IF EXISTS \"ix_samples_name\"",
                "ALTER TABLE \"samples\" RENAME COLUMN \"remark\" TO \"note\"",
                "ALTER TABLE \"samples\" DROP COLUMN IF EXISTS \"note\"",
                "DROP TABLE IF EXISTS \"samples\"",
            ],
            down);
    }

    [Fact(DisplayName = "a migration with a lossy or an unknown operation derives no Down and keeps the refusal that names the type")]
    public void Lossy_and_unknown_operations_derive_no_down()
    {
        var lossy = Assert.Throws<NotSupportedException>(() => DownText(new DropsAColumn()));
        var raw = Assert.Throws<NotSupportedException>(() => DownText(new RunsRawSql()));

        Assert.Equal(typeof(DropsAColumn).FullName + " does not override Down.", lossy.Message);
        Assert.Equal(typeof(RunsRawSql).FullName + " does not override Down.", raw.Message);
        Assert.Null(MigrationOps.DeriveDown(Up(new DropsAColumn())));
    }

    [Fact(DisplayName = "an explicit Down is kept and is not replaced by a derived one")]
    public void An_explicit_down_wins()
    {
        Assert.Equal(["ALTER TABLE \"t\" ADD COLUMN IF NOT EXISTS \"a\" text"], DownText(new ExplicitDown()));
    }

    [Fact(DisplayName = "a migration with no operations derives an empty Down")]
    public void An_empty_up_derives_an_empty_down()
    {
        Assert.Empty(MigrationOps.DeriveDown([])!);
    }

    [Fact(DisplayName = "the audit reports a Reversible declaration over an operation that cannot be inverted unless a Down is declared, and an Irreversible declaration over operations that all can be")]
    public void The_audit_compares_the_declaration_with_the_classes()
    {
        var invertible = Up(new CreatesTableAndIndex());
        var lossy = Up(new DropsAColumn());

        Assert.Equal(ReversibilityFinding.None, MigrationOps.Audit(invertible, Reversibility.Reversible, false));
        Assert.Equal(ReversibilityFinding.DeclaredReversibleButNotInvertible, MigrationOps.Audit(lossy, Reversibility.Reversible, false));
        Assert.Equal(ReversibilityFinding.None, MigrationOps.Audit(lossy, Reversibility.Reversible, true));
        Assert.Equal(ReversibilityFinding.None, MigrationOps.Audit(lossy, Reversibility.Irreversible, false));
        Assert.Equal(ReversibilityFinding.DeclaredIrreversibleButInvertible, MigrationOps.Audit(invertible, Reversibility.Irreversible, false));
        Assert.Equal(ReversibilityFinding.None, MigrationOps.Audit([], Reversibility.Irreversible, false));
    }

    [Fact(DisplayName = "the helper conversions derive the same Down text as the hand-written Down helpers")]
    public void Helper_conversions_derive_their_own_down()
    {
        var up = new MigrationBuilder(PostgresProvider.Instance)
            .Add(SoftDeleteColumnDdl.Up("samples", "v1"))
            .Add(LiveOnlyConversionDdl.Up("samples", ["name"], "deleted_at", "uq", "ix", true, "v1"))
            .Add(ForeignKeyActionDdl.Up("samples", "fk", One, "owners", RefOne, ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.NoAction));

        var derived = DdlCompiler.Render(MigrationOps.DeriveDown(up.Nodes)!, PostgresProvider.Instance);
        var written = DdlCompiler.Render(
            [
                ForeignKeyActionDdl.Down("samples", "fk", One, "owners", RefOne, ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.NoAction),
                LiveOnlyConversionDdl.Down("samples", ["name"], "deleted_at", "uq", "ix", true, "v1"),
                SoftDeleteColumnDdl.Down("samples", "v1"),
            ],
            PostgresProvider.Instance);

        Assert.Equal(written, derived);
    }
}

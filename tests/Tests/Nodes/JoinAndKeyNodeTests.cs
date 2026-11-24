using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.Nodes;

public sealed class JoinAndKeyNodeTests
{
    private static string Text(Node tree) => StatementCompiler.Compile(tree, PostgresProvider.Instance, StatementKind.Read).Text;

    [Fact(DisplayName = "a qualified star is the alias, a dot and a star, and an unqualified one is the bare star")]
    public void A_star_may_carry_a_qualifier()
    {
        Assert.Equal("SELECT o.* FROM \"t\" o", Text(new Select([new Star("o")], new TableRef("t", "o"))));
        Assert.Equal("SELECT * FROM \"t\"", Text(new Select([new Star()], new TableRef("t"))));
        Assert.Equal("SELECT o.*, 1 FROM \"t\" o", Text(new Select([new Star("o"), new IntLit(1)], new TableRef("t", "o"))));
    }

    [Fact(DisplayName = "a join is written after the from clause and before the filter, in the order given, and its nodes are children of the select in that order")]
    public void A_join_sits_between_from_and_where()
    {
        var first = new InnerJoin(new TableRef("a", "x"), new Compare(CompareOp.Equal, new ColumnRef("k", new AliasRef("x")), new ColumnRef("k", new AliasRef("o"))));
        var second = new InnerJoin(new TableRef("b", "y"), new Compare(CompareOp.Equal, new ColumnRef("k", new AliasRef("y")), new ColumnRef("k", new AliasRef("x"))));
        var select = new Select([new Star("o")], new TableRef("t", "o"), new Where([new IsNull(new ColumnRef("d", new AliasRef("o")))]), Joins: [first, second]);

        Assert.Equal(
            "SELECT o.* FROM \"t\" o JOIN \"a\" x ON x.\"k\" = o.\"k\" JOIN \"b\" y ON y.\"k\" = x.\"k\" WHERE o.\"d\" IS NULL",
            Text(select));
        var children = select.Children().ToList();
        var where = children.FindIndex(c => c is Where);
        Assert.True(children.IndexOf(first) < children.IndexOf(second));
        Assert.True(children.IndexOf(second) < where);
    }

    [Fact(DisplayName = "a table-level primary key lists its columns, and a table may mix column definitions with it")]
    public void A_table_primary_key_lists_its_columns()
    {
        var table = new CreateTable("j", [
            new ColumnDef("a", new TypeRef(ColumnType.Uuid), false),
            new ColumnDef("b", new TypeRef(ColumnType.Uuid), false),
            new TablePrimaryKey([new ColumnRef("a"), new ColumnRef("b")]),
        ]);

        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS \"j\" (\"a\" uuid NOT NULL, \"b\" uuid NOT NULL, PRIMARY KEY (\"a\", \"b\"))",
            DdlCompiler.Render([table], PostgresProvider.Instance).Single());
    }
}

using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SqliteRules;

public sealed class SqliteSqlTextTests
{
    [Fact(DisplayName = "a stored table definition is split into column and constraint definitions at the top level only, whatever commas, quotes, comments and nested parentheses sit inside them")]
    public void The_table_is_split_at_the_top_level()
    {
        const string sql = "CREATE TABLE \"a,b\" (\"x,y\" TEXT DEFAULT 'a,b' /* , */, [z] INTEGER, `w` TEXT -- , tail\n, CHECK (x IN (1, 2)), CONSTRAINT \"u, 1\" UNIQUE (\"x,y\", z), PRIMARY KEY (z)) STRICT";

        var parsed = SqliteSqlText.ParseTable(sql);

        Assert.Equal(
            [DefinitionKind.Column, DefinitionKind.Column, DefinitionKind.Column, DefinitionKind.Constraint, DefinitionKind.Constraint, DefinitionKind.Constraint],
            parsed.Definitions.Select(d => d.Kind));
        Assert.Equal(["x,y", "z", "w", null, "u, 1", null], parsed.Definitions.Select(d => d.Name));
        Assert.Equal("\"x,y\" TEXT DEFAULT 'a,b'", parsed.Definitions[0].Text);
        Assert.Equal("CHECK (x IN (1, 2))", parsed.Definitions[3].Text);
        Assert.Equal("STRICT", parsed.Tail);
    }

    [Fact(DisplayName = "a table without options has an empty tail, and a table with no closing parenthesis or no column list is refused rather than half read")]
    public void Malformed_definitions_are_refused()
    {
        Assert.Equal("", SqliteSqlText.ParseTable("CREATE TABLE t (a TEXT)").Tail);
        Assert.Throws<InvalidOperationException>(() => SqliteSqlText.ParseTable("CREATE TABLE t (a TEXT"));
        Assert.Throws<InvalidOperationException>(() => SqliteSqlText.ParseTable("CREATE TABLE t"));
    }

    [Fact(DisplayName = "a foreign key definition is read for its name, columns, parent, parent columns and both actions, with defaults for what it leaves out")]
    public void Foreign_keys_are_read()
    {
        var parsed = SqliteSqlText.ParseTable(
            "CREATE TABLE t (a TEXT, b TEXT, CONSTRAINT \"fk 1\" FOREIGN KEY (a, b) REFERENCES \"o\" (x, y) ON DELETE SET NULL ON UPDATE CASCADE, "
            + "FOREIGN KEY (a) REFERENCES p, CONSTRAINT fk3 FOREIGN KEY (b) REFERENCES q (id) ON UPDATE RESTRICT ON DELETE NO ACTION, UNIQUE (a))");

        var keys = parsed.Definitions.Where(SqliteSqlText.IsForeignKey).Select(SqliteSqlText.ParseForeignKey).ToList();

        Assert.Equal(3, keys.Count);
        Assert.Equal(("fk 1", "o", ReferentialAction.SetNull, ReferentialAction.Cascade), (keys[0].Name, keys[0].ReferencedTable, keys[0].OnDelete, keys[0].OnUpdate));
        Assert.Equal(["a", "b"], keys[0].Columns);
        Assert.Equal(["x", "y"], keys[0].ReferencedColumns);
        Assert.Equal((null, "p", ReferentialAction.NoAction, ReferentialAction.NoAction), (keys[1].Name, keys[1].ReferencedTable, keys[1].OnDelete, keys[1].OnUpdate));
        Assert.Empty(keys[1].ReferencedColumns);
        Assert.Equal(("fk3", ReferentialAction.NoAction, ReferentialAction.Restrict), (keys[2].Name, keys[2].OnDelete, keys[2].OnUpdate));
        Assert.False(SqliteSqlText.IsForeignKey(parsed.Definitions[^1]));
    }

    [Fact(DisplayName = "an action the library does not model is refused by name instead of being read as another one")]
    public void An_unmodelled_action_is_refused()
    {
        var parsed = SqliteSqlText.ParseTable("CREATE TABLE t (a TEXT, FOREIGN KEY (a) REFERENCES p (id) ON DELETE SET DEFAULT)");

        Assert.Throws<NotSupportedException>(() => SqliteSqlText.ParseForeignKey(parsed.Definitions[1]));
        Assert.Throws<NotSupportedException>(() => SqliteSqlText.ActionOf("SET DEFAULT"));
        Assert.Equal(ReferentialAction.SetNull, SqliteSqlText.ActionOf("set null"));
    }

    [Theory(DisplayName = "an index text names a column when the column sits in its keys, its expressions or its predicate, and not when only a literal, the index or the table is called the same")]
    [InlineData("CREATE INDEX ix ON t (a, b)", "b", true)]
    [InlineData("CREATE INDEX ix ON t (lower(b))", "b", true)]
    [InlineData("CREATE INDEX ix ON t (a) WHERE b IS NOT NULL", "b", true)]
    [InlineData("CREATE INDEX ix ON t (a) WHERE \"B\" = 1", "b", true)]
    [InlineData("CREATE INDEX ix ON t (a) WHERE a = 'b'", "b", false)]
    [InlineData("CREATE INDEX b ON t (a)", "b", false)]
    [InlineData("CREATE INDEX ix ON b (a)", "b", false)]
    public void Index_text_mentions(string sql, string column, bool expected)
    {
        Assert.Equal(expected, SqliteSqlText.MentionsInKeysOrPredicate(sql, column));
    }

    [Theory(DisplayName = "a trigger or view text mentions a table by a bare or quoted name in any case, and never by a string literal")]
    [InlineData("CREATE TRIGGER g AFTER INSERT ON o BEGIN UPDATE t SET n = 1; END", true)]
    [InlineData("CREATE TRIGGER g AFTER INSERT ON o BEGIN UPDATE \"T\" SET n = 1; END", true)]
    [InlineData("CREATE TRIGGER g AFTER INSERT ON o BEGIN SELECT 't'; END", false)]
    [InlineData("CREATE TRIGGER g AFTER INSERT ON o BEGIN SELECT 1; END", false)]
    public void Object_text_mentions_a_table(string sql, bool expected)
    {
        Assert.Equal(expected, SqliteSqlText.Mentions(sql, "t"));
    }

    [Fact(DisplayName = "the tokenizer keeps doubled quotes inside an identifier and a literal, and reports an unterminated one as far as it goes")]
    public void Quotes_are_read_whole()
    {
        var tokens = SqliteSqlText.Tokenize("\"a\"\"b\" 'it''s' [x y] `q` 'open");

        Assert.Equal(["a\"b", "it's", "x y", "q", "open"], tokens.Select(t => t.Value));
        Assert.Equal(
            [SqlTokenKind.Identifier, SqlTokenKind.Literal, SqlTokenKind.Identifier, SqlTokenKind.Identifier, SqlTokenKind.Literal],
            tokens.Select(t => t.Kind));
    }
}

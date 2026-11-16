using System;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public class SqliteDialectTests
{
    [Fact]
    public void Quote_wraps_a_plain_identifier_in_double_quotes()
    {
        ISqlDialect d = new SqliteDialect();
        Assert.Equal("\"fixture_widgets\"", d.Quote("fixture_widgets"));
    }

    [Fact]
    public void Quote_escapes_an_embedded_double_quote_by_doubling_it()
    {
        ISqlDialect d = new SqliteDialect();
        Assert.Equal("\"a\"\"b\"", d.Quote("a\"b"));
    }

    [Fact]
    public void Quote_matches_PgDialects_quoting_byte_for_byte()
    {
        ISqlDialect sqlite = new SqliteDialect();
        ISqlDialect pg = new PgDialect();
        foreach (var id in new[] { "fixture_widgets", "a\"b", "\"\"", "" })
            Assert.Equal(pg.Quote(id), sqlite.Quote(id));
    }

    [Fact]
    public void Param_renders_at_p_plus_index()
    {
        ISqlDialect d = new SqliteDialect();
        Assert.Equal("@p0", d.Param(0));
        Assert.Equal("@p7", d.Param(7));
    }

    [Fact]
    public void Page_renders_limit_and_offset_matching_PgDialect()
    {
        ISqlDialect sqlite = new SqliteDialect();
        ISqlDialect pg = new PgDialect();
        Assert.Equal(pg.Page(10, 5), sqlite.Page(10, 5));
        Assert.Equal(pg.Page(1, null), sqlite.Page(1, null));
        Assert.Equal(pg.Page(null, null), sqlite.Page(null, null));
        Assert.Equal(" LIMIT 10 OFFSET 5", sqlite.Page(10, 5));
    }

    [Fact]
    public void SupportsUpsert_and_SupportsReturning_are_both_false()
    {
        ISqlDialect d = new SqliteDialect();
        Assert.False(d.SupportsUpsert);
        Assert.False(d.SupportsReturning);
    }

    [Fact]
    public void ConflictClause_throws_NotSupportedException()
    {
        ISqlDialect d = new SqliteDialect();
        Assert.Throws<NotSupportedException>(() =>
            d.ConflictClause(new[] { "id" }, OnConflict.DoNothing, Array.Empty<UpsertSet>(), "fixture_widgets"));
    }

    [Fact]
    public void ReturningClause_throws_NotSupportedException()
    {
        ISqlDialect d = new SqliteDialect();
        Assert.Throws<NotSupportedException>(() => d.ReturningClause(new[] { "id" }));
    }
}

using Neemle.Orm.Lite.Tests.Behaviour;
using Neemle.Orm.Lite.Tests.Corpus;
using Xunit;

namespace Neemle.Orm.Lite.Tests.QueryTerminalState;

public sealed class FirstRowWireTests
{
    private static (Query<BehaveNote> Query, RecordingLog Log) Notes(ISqlDialect dialect)
    {
        BehaveWorld.Register();
        var log = new RecordingLog();
        var context = TestContext.Of(new RecordingDataSource(log, "wire"), dialect);
        return (new Query<BehaveNote>(context, BehaveNote.Map), log);
    }

    private static List<string> Statements(RecordingLog log) => log.Lines
        .Select(line => line.Split('\n'))
        .Select(parts => parts[Array.IndexOf(parts, "    sql:") + 1].Trim())
        .ToList();

    private static string Before(string statement, string marker) =>
        statement[..statement.IndexOf(marker, StringComparison.Ordinal)];

    [Fact(DisplayName = "AC2: on PostgreSQL a locked, redacted Take(5) query reads LIMIT 1 FOR UPDATE for " +
        "FirstOrDefaultAsync and LIMIT 5 FOR UPDATE for ToListAsync, with the same columns and the same filter")]
    public async Task First_and_list_share_the_columns_and_differ_in_the_limit()
    {
        var (query, log) = Notes(new PgDialect());
        query.Where(n => n.Stars > 0).Take(5).Redact(n => n.Doc).ForUpdate();

        Assert.Null(await query.FirstOrDefaultAsync());
        Assert.Empty(await query.ToListAsync());

        var statements = Statements(log);
        Assert.Equal(2, statements.Count);
        Assert.EndsWith(" LIMIT 1 FOR UPDATE", statements[0], StringComparison.Ordinal);
        Assert.EndsWith(" LIMIT 5 FOR UPDATE", statements[1], StringComparison.Ordinal);
        Assert.Equal(Before(statements[1], " LIMIT "), Before(statements[0], " LIMIT "));
        Assert.Contains("NULL", Before(statements[0], " FROM "), StringComparison.Ordinal);
        Assert.Contains("\"deleted_at\" IS NULL", statements[0], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC2: on PostgreSQL the key-preserving row lock is kept by FirstOrDefaultAsync in its own " +
        "form")]
    public async Task The_key_preserving_lock_is_kept()
    {
        var (query, log) = Notes(new PgDialect());
        query.Where(n => n.Stars > 0).ForNoKeyUpdate();

        await query.FirstOrDefaultAsync();

        Assert.EndsWith(" LIMIT 1 FOR NO KEY UPDATE", Statements(log).Single(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC1: FirstOrDefaultAsync twice sends the same statement both times, and a later read of the " +
        "same query is not limited")]
    public async Task First_twice_sends_one_statement_and_the_list_after_it_is_unlimited()
    {
        var (query, log) = Notes(new SqliteDialect());
        query.Where(n => n.Stars > 0).OrderBy(n => n.Title);

        await query.FirstOrDefaultAsync();
        await query.FirstOrDefaultAsync();
        await query.ToListAsync();

        var statements = Statements(log);
        Assert.Equal(statements[0], statements[1]);
        Assert.EndsWith(" ORDER BY \"title\" ASC NULLS LAST LIMIT 1", statements[0], StringComparison.Ordinal);
        Assert.EndsWith(" ORDER BY \"title\" ASC NULLS LAST", statements[2], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC2: after Take(0) FirstOrDefaultAsync sends LIMIT 0 and a later ToListAsync still sends " +
        "LIMIT 0")]
    public async Task Take_zero_is_kept_by_the_query()
    {
        var (query, log) = Notes(new SqliteDialect());
        query.Where(n => n.Stars > 0).Take(0);

        await query.FirstOrDefaultAsync();
        await query.ToListAsync();

        Assert.All(Statements(log), s => Assert.EndsWith(" LIMIT 0", s, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "[SEC] AC4: FirstOrDefaultAsync after a rendered purge still carries the live term of the " +
        "soft-delete scope")]
    public async Task A_rendered_purge_does_not_widen_a_later_first_row_read()
    {
        var (query, log) = Notes(new PgDialect());
        query.Where(n => n.Stars > 0);

        var purge = query.RenderPurge();
        await query.FirstOrDefaultAsync();

        Assert.DoesNotContain("deleted_at", purge, StringComparison.Ordinal);
        Assert.Contains("\"deleted_at\" IS NULL", Statements(log).Single(), StringComparison.Ordinal);
    }
}

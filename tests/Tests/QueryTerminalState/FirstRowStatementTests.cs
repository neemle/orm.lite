using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.QueryTerminalState;

public sealed class FirstRowStatementTests
{
    private const string Table = "behave_notes";

    private const string Head = "SELECT * FROM \"behave_notes\"";

    private static ISqlProvider Provider(string id) => id switch
    {
        "postgres" => PostgresProvider.Instance,
        "sqlite" => FixtureProvider.Instance,
        "sqlite-real" => SqliteProvider.Instance,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "unknown provider"),
    };

    private static string Text(string provider, SelectClauses clauses, bool? lockRows = null)
    {
        var select = lockRows is bool locked
            ? clauses.BuildFirst([new Star()], null, locked)
            : clauses.BuildFirst([new Star()], null);
        return StatementCompiler.Compile(select, Provider(provider), StatementKind.Read, Table).Text;
    }

    private static SelectClauses Ordered(int? limit = null, int? offset = null)
    {
        var clauses = new SelectClauses(Table) { Limit = limit, Offset = offset };
        clauses.AddOrder(new OrderTerm(new ColumnRef("stars"), false));
        return clauses;
    }

    [Theory(DisplayName = "AC2: a first-row statement of a query with no clause is the clause-free LIMIT 1 read")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public void A_clause_free_first_row_read_limits_to_one(string provider) =>
        Assert.Equal(Head + " LIMIT 1", Text(provider, new SelectClauses(Table)));

    [Theory(DisplayName = "AC2: the limit of a first-row statement is the smaller of one and the Take, so Take(0) " +
        "reads no row")]
    [InlineData("postgres", 5, "LIMIT 1")]
    [InlineData("postgres", 1, "LIMIT 1")]
    [InlineData("postgres", 0, "LIMIT 0")]
    [InlineData("sqlite", 5, "LIMIT 1")]
    [InlineData("sqlite", 1, "LIMIT 1")]
    [InlineData("sqlite", 0, "LIMIT 0")]
    public void The_limit_is_the_smaller_of_one_and_the_take(string provider, int take, string limit) =>
        Assert.Equal(Head + " " + limit, Text(provider, new SelectClauses(Table) { Limit = take }));

    [Theory(DisplayName = "AC2: a first-row statement keeps the order and the offset of the query and limits to one " +
        "row after the skipped ones")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public void The_order_and_the_offset_are_kept(string provider) =>
        Assert.Equal(Head + " ORDER BY \"stars\" ASC LIMIT 1 OFFSET 1", Text(provider, Ordered(limit: 9, offset: 1)));

    [Fact(DisplayName = "AC2: with the row lock asked for, a PostgreSQL first-row statement ends with the row lock, " +
        "after the paging clauses")]
    public void The_row_lock_is_kept_when_asked_for()
    {
        var update = new SelectClauses(Table);
        update.LockRows();
        var keyPreserving = new SelectClauses(Table);
        keyPreserving.LockRowsKeyPreserving();

        Assert.Equal(Head + " LIMIT 1 FOR UPDATE", Text("postgres", update, lockRows: true));
        Assert.Equal(Head + " LIMIT 1 FOR NO KEY UPDATE", Text("postgres", keyPreserving, lockRows: true));
    }

    [Fact(DisplayName = "AC2: the row lock is left out when the caller does not ask for it, and not asking is the " +
        "default")]
    public void The_row_lock_is_left_out_by_default()
    {
        var clauses = new SelectClauses(Table);
        clauses.LockRows();

        Assert.Equal(Head + " LIMIT 1", Text("postgres", clauses, lockRows: false));
        Assert.Equal(Head + " LIMIT 1", Text("postgres", clauses));
    }

    [Fact(DisplayName = "AC2: the SQLite provider spells no row lock, so a locked first-row read is the plain LIMIT " +
        "1 read there")]
    public void The_sqlite_provider_drops_the_row_lock()
    {
        var clauses = new SelectClauses(Table);
        clauses.LockRows();

        Assert.Equal(Head + " LIMIT 1", Text("sqlite-real", clauses, lockRows: true).TrimEnd());
    }

    [Fact(DisplayName = "AC1: composing a first-row statement writes nothing to the clause set")]
    public void Composing_a_first_row_statement_leaves_the_clauses_as_they_were()
    {
        var clauses = Ordered(limit: 5, offset: 1);
        clauses.LockRows();

        Text("postgres", clauses, lockRows: true);

        Assert.Equal(5, clauses.Limit);
        Assert.Equal(1, clauses.Offset);
        Assert.Equal(
            QueryClauses.Take | QueryClauses.Skip | QueryClauses.OrderBy | QueryClauses.RowLock, clauses.Present);
    }
}

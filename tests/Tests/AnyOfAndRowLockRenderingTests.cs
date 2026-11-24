using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Neemle.OrmLite;
using Xunit;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests;

public class AnyOfAndRowLockRenderingTests
{
    private sealed record AnyOfUnlisted(Guid Id, string Name);

    static AnyOfAndRowLockRenderingTests()
    {
        OrmMeta.Register<FixtureWidget>("fixture_widgets", Col, ["id", "name", "rank"]);
        OrmMeta.Register<AnyOfUnlisted>("anyof_unlisted", Col, ["id"]);
    }

    private static string Col(string property) => property.ToLowerInvariant();

    private static readonly Guid Probe = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static OrmContext Pg() => TestContext.Of(new UnreachableDataSource(), PostgresProvider.Instance);

    private static Query<FixtureWidget> Widgets() =>
        new(Pg(), _ => throw new NotSupportedException());

    private static Query<FixtureWidget> Sqlite() =>
        new(TestContext.Of(new UnreachableDataSource(), FixtureProvider.Instance), _ => throw new NotSupportedException());

    [Fact(DisplayName = "Db.AnyOf binds the WHOLE collection as ONE array parameter, where the existing Contains translation expands it into one placeholder per element — an unbounded id set otherwise runs at the driver's parameter limit")]
    public void AnyOf_binds_one_array_parameter_where_Contains_binds_one_per_element()
    {
        var ids = new List<int> { 1, 2, 3 };

        Assert.Equal(
            "DELETE FROM \"fixture_widgets\" WHERE \"rank\" = ANY(@p0)",
            Widgets().Where(w => Db.AnyOf(w.Rank, ids)).RenderDelete());

        Assert.Equal(
            "DELETE FROM \"fixture_widgets\" WHERE \"rank\" IN (@p0, @p1, @p2)",
            Widgets().Where(w => ids.Contains(w.Rank)).RenderDelete());
    }

    [Fact(DisplayName = "a negated Db.AnyOf is the <> ALL(...) the board reconfiguration used, and it stays one array parameter")]
    public void A_negated_AnyOf_is_the_all_form()
    {
        var names = new List<string> { "a", "b" };

        Assert.Equal(
            "DELETE FROM \"fixture_widgets\" WHERE NOT (\"name\" = ANY(@p0))",
            Widgets().Where(w => !Db.AnyOf(w.Name, names)).RenderDelete());
    }

    [Fact(DisplayName = "SANITIZATION PROBE — Db.AnyOf's collection is a bind parameter and never text: a member whose value is a SQL fragment appears nowhere in the statement")]
    public void AnyOf_never_renders_its_values()
    {
        var hostile = new List<string> { "x\") OR 1=1 --" };

        var sql = Widgets().Where(w => Db.AnyOf(w.Name, hostile)).RenderDelete();

        Assert.Equal("DELETE FROM \"fixture_widgets\" WHERE \"name\" = ANY(@p0)", sql);
        Assert.DoesNotContain("OR 1=1", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Db.AnyOf refuses a lazy sequence rather than binding something the driver cannot type — the collection must already be materialised for its element type to be inferred")]
    public void AnyOf_refuses_a_lazy_sequence()
    {
        IEnumerable<int> lazy = Lazy();

        Assert.Throws<NotSupportedException>(() => Widgets().Where(w => Db.AnyOf(w.Rank, lazy)).RenderDelete());

        static IEnumerable<int> Lazy() { yield return 1; }
    }

    [Fact(DisplayName = "Db.NullSafeEquals emits IS NOT DISTINCT FROM and keeps the value a bind parameter, so a NULL scope matches a NULL column instead of never matching")]
    public void NullSafeEquals_emits_is_not_distinct_from()
    {
        string? none = null;

        Assert.Equal(
            "DELETE FROM \"fixture_widgets\" WHERE \"name\" IS NOT DISTINCT FROM @p0",
            Widgets().Where(w => Db.NullSafeEquals(w.Name, none)).RenderDelete());
    }

    [Fact(DisplayName = "SANITIZATION PROBE — Db.NullSafeEquals binds its right-hand side: a value that is a SQL fragment is a parameter and appears nowhere in the statement")]
    public void NullSafeEquals_never_renders_its_value()
    {
        var hostile = "x\" OR \"1\"=\"1";

        var sql = Widgets().Where(w => Db.NullSafeEquals(w.Name, hostile)).RenderDelete();

        Assert.Equal("DELETE FROM \"fixture_widgets\" WHERE \"name\" IS NOT DISTINCT FROM @p0", sql);
        Assert.DoesNotContain("OR", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "SetPredicate assigns a TRANSLATED boolean, so a computed single-default assignment is one statement and the compared value is still a bind parameter")]
    public void SetPredicate_assigns_a_translated_boolean()
    {
        Assert.Equal(
            "UPDATE \"fixture_widgets\" SET \"name\" = (\"id\" = @p0) WHERE \"rank\" = @p1",
            Widgets().SetPredicate(w => w.Name, w => w.Id == Probe).Where(w => w.Rank == 1).RenderUpdate());
    }

    [Fact(DisplayName = "SetColumn assigns one column to another with both sides quoted, which the value-taking Set cannot do without first reading every row it is about to write")]
    public void SetColumn_assigns_column_to_column()
    {
        Assert.Equal(
            "UPDATE \"fixture_widgets\" SET \"name\" = \"id\" WHERE \"rank\" = @p0",
            Widgets().SetColumn(w => w.Name, w => w.Id).Where(w => w.Rank == 1).RenderUpdate());
    }

    [Fact(DisplayName = "ForUpdate appends the row lock LAST, after any paging clause, which is where PostgreSQL requires it")]
    public void ForUpdate_appends_the_row_lock_last()
    {
        Assert.Equal(
            "SELECT \"name\" FROM \"fixture_widgets\" WHERE \"rank\" = @p0 LIMIT 2 FOR UPDATE",
            Widgets().Where(w => w.Rank == 1).Take(2).ForUpdate().RenderProject([w => w.Name]));

        Assert.EndsWith(
            "FOR UPDATE",
            Widgets().Where(w => w.Rank == 1).ForUpdate().RenderSelect(),
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a dialect without row locking REFUSES ForUpdate by name rather than emitting an unlocked read the caller would still treat as serialised")]
    public void A_dialect_without_row_locking_refuses_for_update() =>
        Assert.ThrowsAny<NotSupportedException>(() => Sqlite().Where(w => w.Rank == 1).ForUpdate());

    [Fact(DisplayName = "a dialect without an array parameter refuses Db.AnyOf, and one without null-safe equality refuses Db.NullSafeEquals — rewriting the latter as = is the silent narrowing the vault lookup exists to avoid")]
    public void A_dialect_without_the_capability_refuses_it()
    {
        var ids = new List<int> { 1 };
        string? none = null;

        Assert.ThrowsAny<NotSupportedException>(() => Sqlite().Where(w => Db.AnyOf(w.Rank, ids)).RenderDelete());
        Assert.ThrowsAny<NotSupportedException>(
            () => Sqlite().Where(w => Db.NullSafeEquals(w.Name, none)).RenderDelete());
        Assert.ThrowsAny<NotSupportedException>(() => Sqlite().AggregateCountWhere(w => w.Rank == 1));
    }

    [Fact(DisplayName = "OrderByPredicate orders by a translated boolean and ThenByPredicate appends, so an ordering over a computed scope needs no computed column in the SELECT list")]
    public void OrderByPredicate_orders_by_a_translated_boolean() =>
        Assert.Equal(
            "SELECT \"name\" FROM \"fixture_widgets\" ORDER BY (\"name\" IS NOT NULL) ASC NULLS LAST, "
            + "(\"rank\" = @p0) ASC NULLS LAST",
            Widgets().OrderByPredicate(w => w.Name != null).ThenByPredicate(w => w.Rank == 3)
                .RenderProject([w => w.Name]));

    [Fact(DisplayName = "the aggregate list is ONE statement over ONE predicate and ONE scan, in the order the terms were added — four separate reads would see four snapshots and stop reconciling")]
    public void The_aggregate_list_is_one_statement_over_one_scan()
    {
        var sql = Widgets().Where(w => w.Rank == 1)
            .AggregateSum(w => w.Rank).AggregateCount().AggregateCountDistinct(w => w.Name)
            .AggregateCountWhere(w => w.Name == null)
            .RenderAggregates();

        Assert.Equal(
            "SELECT COALESCE(SUM(COALESCE(\"rank\", 0)), 0), count(*), count(DISTINCT \"name\"), "
            + "count(*) FILTER (WHERE (\"name\" IS NULL)) FROM \"fixture_widgets\" WHERE \"rank\" = @p0",
            sql);
        Assert.Equal(1, sql.Split(" FROM ").Length - 1);
    }

    [Fact(DisplayName = "RenderAggregates refuses an empty term list rather than degenerating into SELECT FROM")]
    public void RenderAggregates_refuses_an_empty_term_list() =>
        Assert.Throws<InvalidOperationException>(() => Widgets().RenderAggregates());

    [Fact(DisplayName = "a WhereIn subquery that binds parameters is OFFSET past the outer query's, so the two never collide — asserted with binds on BOTH sides, since a parameterless subquery would pass against no offset at all")]
    public void A_subquery_offsets_its_parameters_past_the_outer_query()
    {
        var sql = Widgets()
            .Where(w => w.Rank == 10)
            .WhereIn<FixtureWidget>(
                w => w.Id,
                inner => inner.Where(x => x.Name == "keep").OrderBy(x => x.Id).Take(5),
                x => x.Id)
            .SetColumn(w => w.Name, w => w.Id)
            .RenderUpdate();

        Assert.Equal(
            "UPDATE \"fixture_widgets\" SET \"name\" = \"id\" WHERE \"rank\" = @p0 AND \"id\" IN "
            + "(SELECT \"id\" FROM \"fixture_widgets\" WHERE \"name\" = @p1 ORDER BY \"id\" ASC NULLS LAST LIMIT 5)",
            sql);
    }

    [Fact(DisplayName = "RenderInsertMany emits ONE insert with one VALUES tuple per row, and the dialect's now() appears once per row binding nothing — the alternative was N round trips inside a transaction holding its locks")]
    public void RenderInsertMany_emits_one_insert_with_a_tuple_per_row()
    {
        var (sql, ps) = Widgets().RenderInsertMany(
            [
                new Row<FixtureWidget>().With(w => w.Id, 1).With(w => w.Rank, 10).WithNow(w => w.Name),
                new Row<FixtureWidget>().With(w => w.Id, 2).With(w => w.Rank, 20).WithNow(w => w.Name),
            ]);

        Assert.Equal(
            "INSERT INTO \"fixture_widgets\" (\"id\", \"rank\", \"name\") VALUES "
            + "(@p0, @p1, now()), (@p2, @p3, now())",
            sql);
        Assert.Equal(new object?[] { 1, 10, 2, 20 }, ps);
    }

    [Fact(DisplayName = "RenderInsertMany refuses a row naming a different column set — otherwise a mismatched row binds a value into the WRONG column instead of failing")]
    public void RenderInsertMany_refuses_a_row_with_a_different_column_set()
    {
        Assert.Throws<ArgumentException>(() => Widgets().RenderInsertMany(
            [
                new Row<FixtureWidget>().With(w => w.Id, 1).With(w => w.Rank, 10),
                new Row<FixtureWidget>().With(w => w.Id, 2).With(w => w.Name, "x"),
            ]));

        Assert.Throws<InvalidOperationException>(() =>
            Widgets().RenderInsertMany(Array.Empty<Row<FixtureWidget>>()));
    }

    [Fact(DisplayName = "SANITIZATION PROBE — RenderInsertMany's database-clock column lambdas are checked against the entity's REGISTERED columns, so a property OrmMeta does not list is refused before any statement is rendered")]
    public void InsertMany_clock_columns_may_name_only_registered_columns()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Query<AnyOfUnlisted>(Pg(), _ => throw new NotSupportedException()).RenderInsertMany(
                [new Row<AnyOfUnlisted>().With(u => u.Id, 1).WithNow(u => u.Name)]));

        Assert.Contains("not a registered column", ex.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "UpsertSet.Now names the DIALECT's clock in the DO UPDATE arm and binds nothing, so an upsert whose conflict arm refreshes a timestamp reads one clock whichever arm it takes")]
    public void UpsertSet_Now_emits_the_dialect_clock_in_the_conflict_arm()
    {
        var (sql, ps) = Widgets().RenderUpsert(
            new Row<FixtureWidget>().With(w => w.Id, 1).With(w => w.Rank, 2),
            [w => w.Id], OnConflict.DoUpdate,
            sets: [UpsertSet<FixtureWidget>.Inserted(w => w.Rank), UpsertSet<FixtureWidget>.Now(w => w.Name)]);

        Assert.Equal(
            "INSERT INTO \"fixture_widgets\" (\"id\", \"rank\") VALUES (@p0, @p1) "
            + "ON CONFLICT (\"id\") DO UPDATE SET \"rank\" = @p2, \"name\" = now()",
            sql);
        Assert.Equal(3, ps.Count);
    }

    [Fact(DisplayName = "the ping statement is the provider's own spelling of SELECT 1 rather than a text each caller writes, which is what made a liveness probe expressible through the ORM at all")]
    public void The_ping_statement_is_the_providers_select_one()
    {
        Assert.Equal(
            "SELECT 1",
            StatementCompiler.Compile(new Select([new IntLit(1)]), PostgresProvider.Instance, StatementKind.Read).Text);
    }

    [Fact(DisplayName = "every renderer added this ticket opens NO connection, so the pins above are statements rather than round trips")]
    public void The_new_renderers_open_no_connection()
    {
        var source = new UnreachableDataSource();
        Query<FixtureWidget> Fresh() => new(
            TestContext.Of(source, PostgresProvider.Instance), _ => throw new NotSupportedException());
        var q = Fresh();

        q.Where(w => w.Rank == 1).ForUpdate().RenderSelect();
        Fresh().Where(w => w.Rank == 1).AggregateCount().RenderAggregates();
        q.RenderInsertMany([new Row<FixtureWidget>().With(w => w.Id, 1)]);

        Assert.Equal(0, source.Touches);
    }
}

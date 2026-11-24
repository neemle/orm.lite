using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public class IntervalAndProjectionRenderingTests
{
    public sealed record Lease(Guid Id, Guid ItemId, string Holder, DateTime ExpiresAt, DateTime? ReleasedAt);

    public sealed record Item(Guid Id, bool Ready);

    static IntervalAndProjectionRenderingTests()
    {
        OrmMeta.Register<Lease>("interval_leases", Col, ["id", "itemid", "holder", "expiresat", "releasedat"]);
        OrmMeta.Register<Item>("interval_items", Col, ["id", "ready"]);
    }

    private static string Col(string property) => property.ToLowerInvariant();

    private static Query<Lease> Leases() =>
        new(TestContext.Of(new NeverUsedDataSource(), new PgDialect()), _ => throw new NotSupportedException());

    private static Query<Lease> SqliteLeases() =>
        new(TestContext.Of(new NeverUsedDataSource(), new SqliteDialect()), _ => throw new NotSupportedException());

    [Fact(DisplayName = "SetNowPlusMinutes offsets the DATABASE clock by a BOUND minute count, so a lease expiry is minted by the same server that later compares it against now()")]
    public void SetNowPlusMinutes_offsets_the_database_clock()
    {
        Assert.Equal(
            "UPDATE \"interval_leases\" SET \"expiresat\" = now() + make_interval(mins => @p0) "
            + "WHERE \"id\" = @p1",
            Leases().SetNowPlusMinutes(l => l.ExpiresAt, 30).Where(l => l.Id == Guid.Empty).RenderUpdate());
    }

    [Fact(DisplayName = "a NEGATIVE minute count is allowed and still binds, because releasing a lease is `set my expiry into the past` and the claim service spells that as a non-positive count")]
    public void SetNowPlusMinutes_allows_a_negative_count()
    {
        var sql = Leases().SetNowPlusMinutes(l => l.ExpiresAt, -1).Where(l => l.Id == Guid.Empty).RenderUpdate();

        Assert.Contains("now() + make_interval(mins => @p0)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("-1", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a dialect without interval arithmetic refuses SetNowPlusMinutes BY NAME rather than emitting PostgreSQL text at a server that cannot parse it")]
    public void A_dialect_without_interval_arithmetic_refuses_by_name()
    {
        var ex = Assert.Throws<NotSupportedException>(
            () => SqliteLeases().SetNowPlusMinutes(l => l.ExpiresAt, 30));

        Assert.Contains("SqliteDialect", ex.Message, StringComparison.Ordinal);
        Assert.Contains("interval arithmetic", ex.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "NO-FRAGMENT-DOOR PROBE — SetNowPlusMinutes has no string-typed parameter at all: the offset is an int and the column is a lambda, so there is no position in the call where a fragment could be written")]
    public void SetNowPlusMinutes_has_no_string_typed_parameter()
    {
        var m = typeof(Query<Lease>).GetMethod(nameof(Query<Lease>.SetNowPlusMinutes))!;

        Assert.Equal(
            [typeof(Expression<Func<Lease, object?>>), typeof(int)],
            m.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [Fact(DisplayName = "ProjectColumn NAMES its columns instead of selecting star, and ProjectPredicate projects a TRANSLATED boolean next to them — the shape the two claim reads needed instead of the join they were recorded as needing")]
    public void A_projection_carries_columns_and_translated_predicates()
    {
        Assert.Equal(
            "SELECT \"id\", (\"releasedat\" IS NOT NULL), (\"expiresat\" > now()) "
            + "FROM \"interval_leases\" WHERE \"itemid\" = @p0",
            Leases()
                .Where(l => l.ItemId == Guid.Empty)
                .ProjectColumn(l => l.Id)
                .ProjectPredicate(l => l.ReleasedAt != null)
                .ProjectPredicate(l => l.ExpiresAt > Db.Now())
                .RenderProjection());
    }

    [Fact(DisplayName = "a projected predicate may be a correlated EXISTS over another entity, which is what replaces the inner join: the outer column is qualified with its own table so the subquery cannot capture the wrong one")]
    public void A_projected_predicate_may_be_a_correlated_exists()
    {
        Assert.Equal(
            "SELECT \"id\", (EXISTS (SELECT 1 FROM \"interval_items\" s0 WHERE "
            + "(s0.\"id\" = \"interval_leases\".\"itemid\" AND s0.\"ready\" = false))) "
            + "FROM \"interval_leases\"",
            Leases()
                .ProjectColumn(l => l.Id)
                .ProjectPredicate(l => Db.Exists<Item>(i => i.Id == l.ItemId && !i.Ready))
                .RenderProjection());
    }

    [Fact(DisplayName = "rendering a projection TWICE yields the identical statement and does not accumulate a second copy of the bound values — the specific defect that made the terms accumulate on the query rather than on the renderer")]
    public void Rendering_a_projection_twice_is_idempotent()
    {
        var q = Leases()
            .Where(l => l.ItemId == Guid.Empty)
            .ProjectColumn(l => l.Id)
            .ProjectPredicate(l => l.ExpiresAt > Db.Now());

        Assert.Equal(q.RenderProjection(), q.RenderProjection());
        Assert.Equal(1, q.RenderProjection().Split("@p").Length - 1);
    }

    [Fact(DisplayName = "a projection with no terms refuses rather than silently rendering SELECT FROM, which PostgreSQL would answer with a syntax error the caller could not attribute")]
    public void An_empty_projection_refuses()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Leases().RenderProjection());

        Assert.Contains("at least one", ex.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "NO-FRAGMENT-DOOR PROBE — a hostile value compared inside a PROJECTED predicate becomes a bind parameter exactly as it does inside a Where, so the projection list is not a second, unguarded translation path")]
    public void A_projected_predicate_binds_its_values()
    {
        var hostile = "x\") OR 1=1 --";

        var sql = Leases()
            .ProjectColumn(l => l.Id)
            .ProjectPredicate(l => l.Holder == hostile)
            .RenderProjection();

        Assert.Equal(
            "SELECT \"id\", (\"holder\" = @p0) FROM \"interval_leases\"",
            sql);
        Assert.DoesNotContain("OR 1=1", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "NO-FRAGMENT-DOOR PROBE — both projection members take Expression lambdas and nothing else, so a column name a caller invented has no position in the call to arrive through")]
    public void The_projection_members_take_lambdas_only()
    {
        Assert.Equal(
            [typeof(Expression<Func<Lease, object?>>)],
            typeof(Query<Lease>).GetMethod(nameof(Query<Lease>.ProjectColumn))!
                .GetParameters().Select(p => p.ParameterType).ToArray());

        Assert.Equal(
            [typeof(Expression<Func<Lease, bool>>)],
            typeof(Query<Lease>).GetMethod(nameof(Query<Lease>.ProjectPredicate))!
                .GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [Fact(DisplayName = "rendering either new capability opens NO connection, so every pin in this file is a statement rather than a round trip")]
    public void Rendering_opens_no_connection()
    {
        var source = new NeverUsedDataSource();
        var ctx = TestContext.Of(source, new PgDialect());

        var update = "";
        var projection = "";
        var touchedTheDataSource = Record.Exception(() =>
        {
            update = new Query<Lease>(ctx, _ => throw new NotSupportedException())
                .SetNowPlusMinutes(l => l.ExpiresAt, 30).Where(l => l.Id == Guid.Empty).RenderUpdate();
            projection = new Query<Lease>(ctx, _ => throw new NotSupportedException())
                .ProjectColumn(l => l.Id).ProjectPredicate(l => l.ExpiresAt > Db.Now()).RenderProjection();
        });

        Assert.True(
            touchedTheDataSource is null,
            "NeverUsedDataSource throws from BOTH CreateDbConnection and ConnectionString, so a throw "
            + "out of these two renders is a render that reached for a connection. That is the whole "
            + "claim of this test, and it is asserted here rather than left to xunit's unhandled-throw "
            + "path so the failure names the claim instead of the plumbing. Got: " + touchedTheDataSource);

        Assert.StartsWith("UPDATE \"interval_leases\"", update, StringComparison.Ordinal);
        Assert.StartsWith("SELECT \"id\"", projection, StringComparison.Ordinal);
    }
}

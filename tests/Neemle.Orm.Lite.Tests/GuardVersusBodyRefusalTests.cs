using System;
using System.Collections.Generic;
using System.Data.Common;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public class GuardVersusBodyRefusalTests
{
    static GuardVersusBodyRefusalTests() =>
        OrmMeta.Register<FixtureWidget>("fixture_widgets", Col, ["id", "name", "rank"]);

    private static string Col(string property) => property.ToLowerInvariant();

    private static Query<FixtureWidget> Sqlite() =>
        new(new OrmContext(new UnopenableDataSource(), new SqliteDialect()), _ => throw new NotSupportedException());

    private static void AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
        string guardedFlag, string builderMember, string guardWording, NotSupportedException refusal)
    {
        Assert.False(
            refusal.Message.Contains(SqliteDialect.RefusalWordingOnlyThisFixturesOwnBodiesUse, StringComparison.Ordinal),
            $"the guard on ISqlDialect.{guardedFlag} inside Query<T>.{builderMember} DID NOT REFUSE. "
            + "Execution reached SqliteDialect's own member and THAT refused instead, which is exactly "
            + "the unreachability claim this fixture makes about itself and the claim under test. "
            + $"Refusal actually raised: {refusal.Message}");

        Assert.True(
            refusal.Message.Contains(guardWording, StringComparison.Ordinal),
            $"something refused, but not the guard on ISqlDialect.{guardedFlag} inside "
            + $"Query<T>.{builderMember}, whose wording contains \"{guardWording}\". "
            + $"Refusal actually raised: {refusal.Message}");
    }

    [Fact(DisplayName = "a dialect without an array parameter is refused BY Query<T>'s SupportsArrayParameter guard, not by SqliteDialect.AnyOfExpression — flipping the flag true left the whole suite green before this test existed")]
    public void An_array_parameter_is_refused_by_the_guard_and_never_reaches_AnyOfExpression()
    {
        var ids = new List<int> { 1 };

        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            nameof(ISqlDialect.SupportsArrayParameter), "Where(Db.AnyOf)", "express this as Contains",
            Assert.Throws<NotSupportedException>(() => Sqlite().Where(w => Db.AnyOf(w.Rank, ids)).RenderDelete()));
    }

    [Fact(DisplayName = "a dialect without null-safe equality is refused BY Query<T>'s SupportsNullSafeEquality guard, not by SqliteDialect.NullSafeEqualsExpression — flipping the flag true left the whole suite green before this test existed")]
    public void Null_safe_equality_is_refused_by_the_guard_and_never_reaches_NullSafeEqualsExpression()
    {
        string? none = null;

        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            nameof(ISqlDialect.SupportsNullSafeEquality), "Where(Db.NullSafeEquals)", "rewriting it as =",
            Assert.Throws<NotSupportedException>(() => Sqlite().Where(w => Db.NullSafeEquals(w.Name, none)).RenderDelete()));
    }

    [Fact(DisplayName = "a dialect without a FILTER aggregate is refused BY Query<T>'s SupportsAggregateFilter guard, not by SqliteDialect.CountFilterExpression — flipping the flag true left the whole suite green before this test existed")]
    public void An_aggregate_filter_is_refused_by_the_guard_and_never_reaches_CountFilterExpression() =>
        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            nameof(ISqlDialect.SupportsAggregateFilter), nameof(Query<FixtureWidget>.AggregateCountWhere),
            "a second statement would read a different snapshot",
            Assert.Throws<NotSupportedException>(() => Sqlite().AggregateCountWhere(w => w.Rank == 1)));

    [Fact(DisplayName = "a dialect without interval arithmetic is refused BY Query<T>'s SupportsIntervalArithmetic guard, not by SqliteDialect.NowPlusMinutesExpression — flipping the flag true left the whole suite green before this test existed")]
    public void Interval_arithmetic_is_refused_by_the_guard_and_never_reaches_NowPlusMinutesExpression() =>
        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            nameof(ISqlDialect.SupportsIntervalArithmetic), nameof(Query<FixtureWidget>.SetNowPlusMinutes),
            "would move the lease clock off the server",
            Assert.Throws<NotSupportedException>(() => Sqlite().SetNowPlusMinutes(w => w.Name, 5)));

    [Fact(DisplayName = "a dialect without a row lock is refused BY Query<T>'s SupportsRowLock guard THROUGH A RENDER, not by SqliteDialect.RowLockClause — asserting only that ForUpdate throws pins call ordering, since the clause it protects is not reached until render")]
    public void A_row_lock_is_refused_by_the_guard_and_never_reaches_RowLockClause_even_at_render_time() =>
        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            nameof(ISqlDialect.SupportsRowLock), nameof(Query<FixtureWidget>.ForUpdate),
            "cannot be served by an unlocked read",
            Assert.Throws<NotSupportedException>(
                () => Sqlite().Where(w => w.Rank == 1).ForUpdate().RenderSelect()));

    [Fact(DisplayName = "a dialect without upsert is refused BY Query<T>'s SupportsUpsert guard, not by SqliteDialect.ConflictClause — the fixture body also contains the words 'has no upsert', so the existing assertion on that phrase alone stayed green with the guard gone")]
    public void An_upsert_is_refused_by_the_guard_and_never_reaches_ConflictClause() =>
        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            nameof(ISqlDialect.SupportsUpsert), nameof(Query<FixtureWidget>.RenderUpsert),
            "express this as an explicit read-modify-write",
            Assert.Throws<NotSupportedException>(() => Sqlite().RenderUpsert(
                new Row<FixtureWidget>().With(w => w.Id, Guid.Empty).With(w => w.Rank, 1),
                [w => w.Id], OnConflict.DoNothing)));

    [Fact(DisplayName = "a dialect without RETURNING is refused BY Query<T>'s SupportsReturning guard on BOTH returning renderers, not by SqliteDialect.ReturningClause — each call satisfies the Set and Where preconditions so the fixture body is genuinely reachable once the guard is gone")]
    public void Returning_is_refused_by_the_guard_and_never_reaches_ReturningClause()
    {
        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            nameof(ISqlDialect.SupportsReturning), nameof(Query<FixtureWidget>.RenderUpdateReturning),
            "select the affected ids first",
            Assert.Throws<NotSupportedException>(
                () => Sqlite().SetColumn(w => w.Name, w => w.Id).Where(w => w.Rank == 1).RenderUpdateReturning([w => w.Id])));

        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            nameof(ISqlDialect.SupportsReturning), nameof(Query<FixtureWidget>.RenderDeleteReturning),
            "select the affected rows first",
            Assert.Throws<NotSupportedException>(
                () => Sqlite().Where(w => w.Rank == 1).RenderDeleteReturning([w => w.Id])));
    }

    [Fact(DisplayName = "all seven throwing members carry the fixture's own refusal wording when called directly, which is what stops the guard tests above from passing vacuously if a message is reworded")]
    public void Every_guarded_member_called_directly_carries_the_fixtures_own_refusal_wording()
    {
        ISqlDialect d = new SqliteDialect();

        var refusals = new List<(string Member, Func<object?> Call)>
        {
            (nameof(ISqlDialect.ConflictClause), () => d.ConflictClause(["id"], OnConflict.DoNothing, [], "fixture_widgets")),
            (nameof(ISqlDialect.ReturningClause), () => d.ReturningClause(["id"])),
            (nameof(ISqlDialect.AnyOfExpression), () => d.AnyOfExpression("\"rank\"", "@p0")),
            (nameof(ISqlDialect.NullSafeEqualsExpression), () => d.NullSafeEqualsExpression("\"name\"", "@p0")),
            (nameof(ISqlDialect.RowLockClause), () => d.RowLockClause),
            (nameof(ISqlDialect.CountFilterExpression), () => d.CountFilterExpression("(\"rank\" = @p0)")),
            (nameof(ISqlDialect.NowPlusMinutesExpression), () => d.NowPlusMinutesExpression("@p0")),
        };

        Assert.Equal(7, refusals.Count);

        foreach (var (member, call) in refusals)
            Assert.True(
                Assert.Throws<NotSupportedException>(call).Message
                    .Contains(SqliteDialect.RefusalWordingOnlyThisFixturesOwnBodiesUse, StringComparison.Ordinal),
                $"SqliteDialect.{member} no longer carries "
                + $"\"{SqliteDialect.RefusalWordingOnlyThisFixturesOwnBodiesUse}\", so every guard-versus-body "
                + "test that forbids that phrase has silently stopped being able to detect a removed guard.");
    }

    private sealed class UnopenableDataSource : DbDataSource
    {
        public override string ConnectionString => "fake://guard";
        protected override DbConnection CreateDbConnection() =>
            throw new InvalidOperationException("these are rendering pins and must not open a connection.");
    }
}

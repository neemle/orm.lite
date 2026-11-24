using System;
using System.Collections.Generic;
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests;

public class GuardVersusBodyRefusalTests
{
    static GuardVersusBodyRefusalTests() =>
        OrmMeta.Register<FixtureWidget>("fixture_widgets", Col, ["id", "name", "rank"]);

    private static string Col(string property) => property.ToLowerInvariant();

    private static Query<FixtureWidget> Sqlite() =>
        new(TestContext.Of(new UnreachableDataSource(), FixtureProvider.Instance), _ => throw new NotSupportedException());

    private static void AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
        Capability guarded, string builderMember, string guardWording, NotSupportedException refusal)
    {
        Assert.False(
            refusal.Message.Contains(FixtureSerializer.RefusalWordingOnlyThisFixturesOwnBodiesUse, StringComparison.Ordinal),
            $"the guard on Capability.{guarded} inside Query<T>.{builderMember} DID NOT REFUSE. "
            + "Execution reached the fixture serializer's own body and THAT refused instead, which is exactly "
            + "the unreachability claim this fixture makes about itself and the claim under test. "
            + $"Refusal actually raised: {refusal.Message}");

        Assert.True(
            refusal.Message.Contains(guardWording, StringComparison.Ordinal),
            $"something refused, but not the guard on Capability.{guarded} inside "
            + $"Query<T>.{builderMember}, whose wording contains \"{guardWording}\". "
            + $"Refusal actually raised: {refusal.Message}");
    }

    [Fact(DisplayName = "a dialect without an array parameter is refused BY Query<T>'s ArrayParameter guard, not by the fixture serializer's AnyOf body — flipping the flag true left the whole suite green before this test existed")]
    public void An_array_parameter_is_refused_by_the_guard_and_never_reaches_AnyOfExpression()
    {
        var ids = new List<int> { 1 };

        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            Capability.ArrayParameter, "Where(Db.AnyOf)", "express this as Contains",
            Assert.ThrowsAny<NotSupportedException>(() => Sqlite().Where(w => Db.AnyOf(w.Rank, ids)).RenderDelete()));
    }

    [Fact(DisplayName = "a dialect without null-safe equality is refused BY Query<T>'s NullSafeEquality guard, not by the fixture serializer — flipping the flag true left the whole suite green before this test existed")]
    public void Null_safe_equality_is_refused_by_the_guard_and_never_reaches_NullSafeEqualsExpression()
    {
        string? none = null;

        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            Capability.NullSafeEquality, "Where(Db.NullSafeEquals)", "rewriting it as =",
            Assert.ThrowsAny<NotSupportedException>(() => Sqlite().Where(w => Db.NullSafeEquals(w.Name, none)).RenderDelete()));
    }

    [Fact(DisplayName = "a dialect without a FILTER aggregate is refused BY Query<T>'s AggregateFilter guard, not by the fixture serializer — flipping the flag true left the whole suite green before this test existed")]
    public void An_aggregate_filter_is_refused_by_the_guard_and_never_reaches_CountFilterExpression() =>
        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            Capability.AggregateFilter, nameof(Query<FixtureWidget>.AggregateCountWhere),
            "a second statement would read a different snapshot",
            Assert.ThrowsAny<NotSupportedException>(() => Sqlite().AggregateCountWhere(w => w.Rank == 1)));

    [Fact(DisplayName = "a dialect without interval arithmetic is refused BY Query<T>'s IntervalArithmetic guard, not by the fixture serializer's ClockPlusMinutes body — flipping the flag true left the whole suite green before this test existed")]
    public void Interval_arithmetic_is_refused_by_the_guard_and_never_reaches_NowPlusMinutesExpression() =>
        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            Capability.IntervalArithmetic, nameof(Query<FixtureWidget>.SetNowPlusMinutes),
            "would move the lease clock off the server",
            Assert.ThrowsAny<NotSupportedException>(() => Sqlite().SetNowPlusMinutes(w => w.Name, 5)));

    [Fact(DisplayName = "a dialect without a row lock is refused BY Query<T>'s RowLock guard THROUGH A RENDER, not by the fixture serializer — asserting only that ForUpdate throws pins call ordering, since the clause it protects is not reached until render")]
    public void A_row_lock_is_refused_by_the_guard_and_never_reaches_RowLockClause_even_at_render_time() =>
        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            Capability.RowLock, nameof(Query<FixtureWidget>.ForUpdate),
            "cannot be served by an unlocked read",
            Assert.ThrowsAny<NotSupportedException>(
                () => Sqlite().Where(w => w.Rank == 1).ForUpdate().RenderSelect()));

    [Fact(DisplayName = "a dialect without upsert is refused BY Query<T>'s Upsert guard, not by the fixture serializer — the fixture body also contains the words 'has no upsert', so the existing assertion on that phrase alone stayed green with the guard gone")]
    public void An_upsert_is_refused_by_the_guard_and_never_reaches_ConflictClause() =>
        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            Capability.Upsert, nameof(Query<FixtureWidget>.RenderUpsert),
            "express this as an explicit read-modify-write",
            Assert.ThrowsAny<NotSupportedException>(() => Sqlite().RenderUpsert(
                new Row<FixtureWidget>().With(w => w.Id, Guid.Empty).With(w => w.Rank, 1),
                [w => w.Id], OnConflict.DoNothing)));

    [Fact(DisplayName = "a dialect without RETURNING is refused BY Query<T>'s Returning guard on BOTH returning renderers, not by the fixture serializer — each call satisfies the Set and Where preconditions so the fixture body is genuinely reachable once the guard is gone")]
    public void Returning_is_refused_by_the_guard_and_never_reaches_ReturningClause()
    {
        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            Capability.Returning, nameof(Query<FixtureWidget>.RenderUpdateReturning),
            "select the affected ids first",
            Assert.ThrowsAny<NotSupportedException>(
                () => Sqlite().SetColumn(w => w.Name, w => w.Id).Where(w => w.Rank == 1).RenderUpdateReturning([w => w.Id])));

        AssertRefusedByTheBuildersGuardAndNotByTheFixtureBody(
            Capability.Returning, nameof(Query<FixtureWidget>.RenderDeleteReturning),
            "select the affected rows first",
            Assert.ThrowsAny<NotSupportedException>(
                () => Sqlite().Where(w => w.Rank == 1).RenderDeleteReturning([w => w.Id])));
    }

    [Fact(DisplayName = "the fixture serializer's bodies carry its own refusal wording when reached directly, which is what stops the guard tests above from passing vacuously if a message is reworded")]
    public void Every_fixture_body_reached_directly_carries_the_fixtures_own_refusal_wording()
    {
        var serializer = new FixtureSerializer();

        var refusals = new List<(string Node, Func<object?> Call)>
        {
            (nameof(AnyOf), () => serializer.Fragment(new AnyOf(new ColumnRef("rank"), new Param(0, null)))),
            (nameof(ClockPlusMinutes), () => serializer.Fragment(new ClockPlusMinutes(new Param(0, null)))),
        };

        foreach (var (node, call) in refusals)
            Assert.True(
                Assert.Throws<NotSupportedException>(call).Message
                    .Contains(FixtureSerializer.RefusalWordingOnlyThisFixturesOwnBodiesUse, StringComparison.Ordinal),
                $"the fixture serializer's {node} body no longer carries "
                + $"\"{FixtureSerializer.RefusalWordingOnlyThisFixturesOwnBodiesUse}\", so every guard-versus-body "
                + "test that forbids that phrase has silently stopped being able to detect a removed guard.");
    }
}

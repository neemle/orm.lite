using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public class ReturningConflictAndClockRenderingTests
{
    private sealed record ReturningUnlisted(Guid Id, string Name);

    static ReturningConflictAndClockRenderingTests()
    {
        OrmMeta.Register<FixtureWidget>("fixture_widgets", Col, ["id", "name", "rank"]);
        OrmMeta.Register<ReturningUnlisted>("returning_unlisted", Col, ["id"]);
    }

    private static string Col(string property) => property.ToLowerInvariant();

    private static OrmContext Pg() => TestContext.Of(new UnreachableDataSource(), new PgDialect());

    private static Query<FixtureWidget> Widgets() =>
        new(Pg(), _ => throw new NotSupportedException());

    [Fact(DisplayName = "DeleteReturningAsync emits one DELETE with the predicate and a RETURNING list, and never a SELECT")]
    public void Delete_returning_emits_one_delete_with_a_returning_list() =>
        Assert.Equal(
            "DELETE FROM \"fixture_widgets\" WHERE \"rank\" = @p0 RETURNING \"id\", \"name\"",
            Widgets().Where(w => w.Rank == 7).RenderDeleteReturning([w => w.Id, w => w.Name]));

    [Fact(DisplayName = "DeleteReturningAsync REFUSES an unfiltered DELETE — a guarantee the hand-written statements it replaced could not carry for themselves")]
    public void Delete_returning_refuses_an_unfiltered_delete() =>
        Assert.Throws<InvalidOperationException>(() => Widgets().RenderDeleteReturning([w => w.Id]));

    [Fact(DisplayName = "UpdateReturningAsync refuses an unfiltered UPDATE too, closing an asymmetry: UpdateAsync and DeleteAsync always refused one, and the RETURNING variant did not — it had no production caller to break, so the guard was added rather than recorded as a hazard")]
    public void Update_returning_refuses_an_unfiltered_update()
    {
        Assert.Throws<InvalidOperationException>(
            () => Widgets().SetCoalesce(w => w.Name, "x").RenderUpdateReturning([w => w.Id]));

        Assert.Equal(
            "UPDATE \"fixture_widgets\" SET \"name\" = COALESCE(@p0, \"name\") WHERE \"rank\" = @p1 RETURNING \"id\"",
            Widgets().SetCoalesce(w => w.Name, "x").Where(w => w.Rank == 1).RenderUpdateReturning([w => w.Id]));
    }

    [Fact(DisplayName = "a RETURNING column lambda may name only a registered column of the entity, so a property OrmMeta does not list is refused before any statement is rendered")]
    public void A_returning_list_may_name_only_registered_columns()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Query<ReturningUnlisted>(Pg(), _ => throw new NotSupportedException())
                .Where(u => u.Id == Guid.Empty).RenderDeleteReturning([u => u.Name]));

        Assert.Contains("not a registered column", ex.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "SetCoalesce keeps the existing value when the parameter is null, and the column is quoted on both sides while the value stays a bind parameter")]
    public void Set_coalesce_emits_a_parameterised_coalesce() =>
        Assert.Equal(
            "UPDATE \"fixture_widgets\" SET \"name\" = COALESCE(@p0, \"name\") WHERE \"rank\" = @p1",
            Widgets().SetCoalesce(w => w.Name, "x").Where(w => w.Rank == 1).RenderUpdate());

    [Fact(DisplayName = "an EMPTY conflict-column list emits an UNTARGETED ON CONFLICT, which is the only way to collapse several unique constraints into one indistinguishable refusal")]
    public void An_empty_conflict_list_emits_an_untargeted_on_conflict()
    {
        var (sql, ps) = Widgets().RenderUpsert(
            new Row<FixtureWidget>().With(w => w.Id, 1).With(w => w.Name, "a"),
            [], OnConflict.DoNothing, returning: [w => w.Id]);

        Assert.Equal(
            "INSERT INTO \"fixture_widgets\" (\"id\", \"name\") VALUES (@p0, @p1) "
            + "ON CONFLICT DO NOTHING RETURNING \"id\"",
            sql);
        Assert.Equal(new object?[] { 1, "a" }, ps);
    }

    [Fact(DisplayName = "a NAMED conflict target still emits ON CONFLICT (cols), so the untargeted form is an addition and not a change to existing upserts")]
    public void A_named_conflict_target_still_emits_its_columns() =>
        Assert.StartsWith(
            "INSERT INTO \"fixture_widgets\" (\"id\") VALUES (@p0) ON CONFLICT (\"id\") DO NOTHING",
            Widgets().RenderUpsert(
                new Row<FixtureWidget>().With(w => w.Id, 1),
                [w => w.Id], OnConflict.DoNothing).Sql,
            StringComparison.Ordinal);

    [Fact(DisplayName = "an untargeted ON CONFLICT cannot DO UPDATE, and the refusal names the reason rather than letting PostgreSQL answer 42601")]
    public void An_untargeted_conflict_cannot_do_update()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Widgets().RenderUpsert(
            new Row<FixtureWidget>().With(w => w.Id, 1),
            [], OnConflict.DoUpdate,
            sets: [UpsertSet<FixtureWidget>.Increment(w => w.Id, 1)]));

        Assert.Contains("conflict target", ex.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "databaseClockColumns puts the DIALECT's now() in the VALUES list rather than an application timestamp, and binds no parameter for it")]
    public void Database_clock_columns_emit_now_and_bind_nothing()
    {
        var (sql, ps) = Widgets().RenderUpsert(
            new Row<FixtureWidget>().With(w => w.Id, 1).WithNow(w => w.Name),
            [], OnConflict.DoNothing);

        Assert.Equal(
            "INSERT INTO \"fixture_widgets\" (\"id\", \"name\") VALUES (@p0, now()) ON CONFLICT DO NOTHING",
            sql);
        Assert.Single(ps);
    }

    [Fact(DisplayName = "ProjectAsync names its columns explicitly instead of SELECT *, so a read can have a disclosure surface narrower than its table")]
    public void Project_names_its_columns_instead_of_selecting_star()
    {
        var sql = Widgets().Where(w => w.Rank == 3).RenderProject([w => w.Name]);

        Assert.Equal("SELECT \"name\" FROM \"fixture_widgets\" WHERE \"rank\" = @p0", sql);
        Assert.DoesNotContain("*", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ProjectAsync refuses an empty column list rather than degenerating into SELECT FROM")]
    public void Project_refuses_an_empty_column_list() =>
        Assert.Throws<InvalidOperationException>(() =>
            Widgets().RenderProject(Array.Empty<Expression<Func<FixtureWidget, object?>>>()));

    [Fact(DisplayName = "ThenBy adds a second ORDER BY term, and OrderBy after it RESETS rather than appending, so an ordering is never half-inherited from an earlier call")]
    public void ThenBy_appends_and_OrderBy_resets()
    {
        Assert.Equal(
            "SELECT \"name\" FROM \"fixture_widgets\" ORDER BY \"rank\" ASC NULLS LAST, \"name\" DESC NULLS LAST",
            Widgets().OrderBy(w => w.Rank).ThenByDescending(w => w.Name).RenderProject([w => w.Name]));

        Assert.Equal(
            "SELECT \"name\" FROM \"fixture_widgets\" ORDER BY \"name\" ASC NULLS LAST",
            Widgets().OrderBy(w => w.Rank).ThenBy(w => w.Name).OrderBy(w => w.Name).RenderProject([w => w.Name]));
    }

    [Fact(DisplayName = "the renderers and the executors share one code path: rendering opens no connection, and the data source records a touch only when a terminal operator runs, so the zero is an observation rather than a fixture that could never move")]
    public async Task Rendering_opens_nothing_but_executing_does()
    {
        var source = new UnreachableDataSource();
        var q = new Query<FixtureWidget>(
            TestContext.Of(source, new PgDialect()), _ => throw new NotSupportedException());

        q.Where(w => w.Rank == 1).RenderDeleteReturning([w => w.Id]);
        Assert.Equal(0, source.Touches);

        await Assert.ThrowsAnyAsync<Exception>(
            () => q.Where(w => w.Rank == 1).DeleteReturningAsync([w => w.Id]));
        Assert.Equal(1, source.Touches);
    }

    [Fact(DisplayName = "a dialect without RETURNING refuses DeleteReturningAsync by name, so the capability is a dialect question and not an assumption baked into the builder")]
    public void A_dialect_without_returning_refuses_delete_returning()
    {
        var q = new Query<FixtureWidget>(
            TestContext.Of(new UnreachableDataSource(), new SqliteDialect()), _ => throw new NotSupportedException());

        Assert.Throws<NotSupportedException>(() => q.Where(w => w.Rank == 1).RenderDeleteReturning([w => w.Id]));
    }
}

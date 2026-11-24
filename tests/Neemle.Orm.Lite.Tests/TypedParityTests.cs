using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Neemle.Orm.Lite;
using Xunit;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests;

public sealed class TypedParityTests
{
    private const string RowTable = "typed_rows";

    private sealed record TypedRow(Guid Id, string Name, int Hits, DateTime? Seen, string Unmapped);

    static TypedParityTests() =>
        OrmMeta.Register<TypedRow>(RowTable, RowCol, ["id", "name", "hits", "seen"]);

    private static string RowCol(string member) => member switch
    {
        nameof(TypedRow.Id) => "id",
        nameof(TypedRow.Name) => "name",
        nameof(TypedRow.Hits) => "hits",
        nameof(TypedRow.Seen) => "seen",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown TypedRow member."),
    };

    private static Query<TypedRow> Q() =>
        new(TestContext.Of(new NeverUsedDataSource(), PostgresProvider.Instance),
            _ => throw new NotSupportedException("no rows are read here"));

    private static Expression<Func<TypedRow, object?>> NotAColumn() => r => r.Name.Length;

    private static Expression<Func<TypedRow, object?>> UnmappedColumn() => r => r.Unmapped;

    private static Row<TypedRow> BuildRow(Guid id, bool withClock)
    {
        var row = new Row<TypedRow>().With(r => r.Id, id).With(r => r.Name, "n").With(r => r.Hits, 3);
        return withClock ? row.WithNow(r => r.Seen) : row;
    }

    [Fact(DisplayName = "a lambda naming an unmapped property, or not selecting a member of its parameter, is refused by every typed entry point before any SQL is rendered")]
    public void An_unmapped_or_non_member_lambda_is_refused()
    {
        var id = Guid.NewGuid();
        var good = new Expression<Func<TypedRow, object?>>[] { r => r.Id };

        foreach (var bad in new[] { UnmappedColumn(), NotAColumn() })
        {
            var oneBad = new[] { bad };
            Assert.ThrowsAny<ArgumentException>(() => Q().RenderUpsert(
                new Row<TypedRow>().With(bad, "x"), good, OnConflict.DoNothing));
            Assert.ThrowsAny<ArgumentException>(() => Q().RenderUpsert(
                new Row<TypedRow>().With(r => r.Id, id).WithNow(bad), good, OnConflict.DoNothing));
            Assert.ThrowsAny<ArgumentException>(() => Q().RenderUpsert(
                BuildRow(id, false), oneBad, OnConflict.DoNothing));
            Assert.ThrowsAny<ArgumentException>(() => Q().RenderUpsert(
                BuildRow(id, false), good, OnConflict.DoNothing, null, oneBad));
            Assert.ThrowsAny<ArgumentException>(() => Q().RenderUpsert(
                BuildRow(id, false), good, OnConflict.DoUpdate, new[] { UpsertSet<TypedRow>.Now(bad) }));
            Assert.ThrowsAny<ArgumentException>(() => Q().RenderInsertMany(
                new[] { new Row<TypedRow>().With(bad, "x") }));
            Assert.ThrowsAny<ArgumentException>(() => Q().Where(r => r.Id == id).RenderUpdateReturning(oneBad));
            Assert.ThrowsAny<ArgumentException>(() => Q().Where(r => r.Id == id).RenderDeleteReturning(oneBad));
        }
    }

    [Fact(DisplayName = "InsertMany over Row<T>s whose value columns or clock columns differ is refused, and a null row or null rows list is refused")]
    public void Differing_insert_many_rows_are_refused()
    {
        var id = Guid.NewGuid();
        var named = new Row<TypedRow>().With(r => r.Id, id).With(r => r.Name, "n");
        var swapped = new Row<TypedRow>().With(r => r.Id, id).With(r => r.Hits, 1);
        var shorter = new Row<TypedRow>().With(r => r.Id, id);

        Assert.ThrowsAny<ArgumentException>(() => Q().RenderInsertMany(new[] { named, swapped }));
        Assert.ThrowsAny<ArgumentException>(() => Q().RenderInsertMany(new[] { named, shorter }));
        Assert.ThrowsAny<ArgumentException>(() => Q().RenderInsertMany(
            new[] { BuildRow(id, true), BuildRow(id, false) }));

        Assert.Throws<ArgumentNullException>(() => Q().RenderInsertMany((IReadOnlyList<Row<TypedRow>>)null!));
        Assert.Throws<ArgumentNullException>(() => Q().RenderUpsert(
            (Row<TypedRow>)null!, new Expression<Func<TypedRow, object?>>[] { r => r.Id }, OnConflict.DoNothing));
    }

    [Fact(DisplayName = "UpsertSet<T>.Inserted for a column the Row does not carry is refused by name before anything is rendered")]
    public void Inserted_for_an_absent_column_is_refused_by_name()
    {
        var row = new Row<TypedRow>().With(r => r.Id, Guid.NewGuid()).With(r => r.Name, "n");

        var error = Assert.Throws<ArgumentException>(() => Q().RenderUpsert(
            row,
            new Expression<Func<TypedRow, object?>>[] { r => r.Id },
            OnConflict.DoUpdate,
            new[] { UpsertSet<TypedRow>.Inserted(r => r.Hits) }));

        Assert.Contains("\"hits\"", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "(OnConflict)99 is refused by name by the typed RenderUpsert and UpsertAsync before anything is rendered")]
    public async Task An_undefined_conflict_action_is_refused_by_the_typed_forms()
    {
        var row = BuildRow(Guid.NewGuid(), false);
        var conflict = new Expression<Func<TypedRow, object?>>[] { r => r.Id };

        var rendered = Assert.Throws<ArgumentOutOfRangeException>(
            () => Q().RenderUpsert(row, conflict, (OnConflict)99));
        Assert.Equal("action", rendered.ParamName);

        var executed = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Q().UpsertAsync(row, conflict, (OnConflict)99));
        Assert.Equal("action", executed.ParamName);
    }
}

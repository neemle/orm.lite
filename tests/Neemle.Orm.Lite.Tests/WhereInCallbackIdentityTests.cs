using System.Data.Common;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public class WhereInCallbackIdentityTests
{
    private const string Table = "callback_identity_rows";
    private const string Refusal = "A WhereIn builder must return the query instance it was given.";

    static WhereInCallbackIdentityTests() =>
        OrmMeta.Register<CallbackRow>(Table, Col, ["id", "name", "rank"]);

    [Fact]
    public void A_foreign_result_is_refused_without_mutating_the_outer_query()
    {
        var outer = Rows().Where(row => row.Rank == 10);

        var error = Assert.Throws<InvalidOperationException>(() => outer.WhereIn<CallbackRow>(
            row => row.Id,
            handed =>
            {
                handed.Where(row => row.Name == "handed");
                return Rows().Where(row => row.Name == "foreign");
            },
            row => row.Id));

        Assert.Equal(Refusal, error.Message);
        Assert.Equal(
            $"DELETE FROM \"{Table}\" WHERE \"rank\" = @p0",
            outer.RenderDelete());
        Assert.Equal(new object?[] { 10 }, outer.BoundParameters);

        outer.Where(row => row.Name == "after");
        Assert.Equal(
            $"DELETE FROM \"{Table}\" WHERE \"rank\" = @p0 AND \"name\" = @p1",
            outer.RenderDelete());
        Assert.Equal(new object?[] { 10, "after" }, outer.BoundParameters);
    }

    [Fact]
    public void A_null_result_has_the_same_refusal_and_leaves_the_outer_query_unchanged()
    {
        var outer = Rows().Where(row => row.Rank == 20);

        var error = Assert.Throws<InvalidOperationException>(() => outer.WhereIn<CallbackRow>(
            row => row.Id,
            handed =>
            {
                handed.Where(row => row.Name == "discarded");
                return null!;
            },
            row => row.Id));

        Assert.Equal(Refusal, error.Message);
        Assert.Equal(
            $"DELETE FROM \"{Table}\" WHERE \"rank\" = @p0",
            outer.RenderDelete());
        Assert.Equal(new object?[] { 20 }, outer.BoundParameters);
    }

    [Fact]
    public void A_callback_exception_propagates_by_identity_without_mutating_the_outer_query()
    {
        var outer = Rows().Where(row => row.Rank == 30);
        var sentinel = new CallbackFailure();

        var error = Assert.Throws<CallbackFailure>(() => outer.WhereIn<CallbackRow>(
            row => row.Id,
            handed =>
            {
                handed.Where(row => row.Name == "discarded");
                throw sentinel;
            },
            row => row.Id));

        Assert.Same(sentinel, error);
        Assert.Equal(
            $"DELETE FROM \"{Table}\" WHERE \"rank\" = @p0",
            outer.RenderDelete());
        Assert.Equal(new object?[] { 30 }, outer.BoundParameters);
    }

    [Fact]
    public void The_handed_instance_keeps_outer_and_inner_values_in_distinct_slots()
    {
        var outer = Rows()
            .Where(row => row.Rank == 40)
            .WhereIn<CallbackRow>(
                row => row.Id,
                handed => handed.Where(row => row.Name == "inside"),
                row => row.Id);

        Assert.Equal(
            $"DELETE FROM \"{Table}\" WHERE \"rank\" = @p0 AND \"id\" IN "
            + $"(SELECT \"id\" FROM \"{Table}\" WHERE \"name\" = @p1)",
            outer.RenderDelete());
        Assert.Equal(new object?[] { 40, "inside" }, outer.BoundParameters);
    }

    private static Query<CallbackRow> Rows() =>
        new(
            new OrmContext(new NeverUsedDataSource(), new PgDialect()),
            _ => throw new NotSupportedException());

    private static string Col(string property) => property.ToLowerInvariant();

    private sealed class CallbackFailure : Exception { }

    private sealed class CallbackRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int Rank { get; set; }
    }
}

using Neemle.Orm.Lite.Tests.SoftDelete.Fixtures;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SoftDelete.PurgeUpsert;

public sealed class SoftUpsertRenderTests
{
    private static readonly Row<SoftRoot> RootValues = new Row<SoftRoot>().With(r => r.Id, 1).With(r => r.Name, "n");
    private static readonly UpsertSet<SoftRoot>[] RootRename = [UpsertSet<SoftRoot>.Inserted(r => r.Name)];
    private static readonly Row<SoftReferrer> ReferrerValues = new Row<SoftReferrer>().With(r => r.Id, 1).With(r => r.Name, "n");
    private static readonly UpsertSet<SoftReferrer>[] ReferrerRename = [UpsertSet<SoftReferrer>.Inserted(r => r.Name)];

    private static Query<SoftReferrer> NonSoftReferrers()
    {
        _ = SoftRootRender.Roots();
        return new Query<SoftReferrer>(
            new OrmContext(new NeverUsedDataSource(), new PgDialect()),
            SoftReferrer.Map);
    }

    [Fact(DisplayName = "AC6: a DoUpdate upsert into a soft entity renders a DO UPDATE guarded by the table-qualified live predicate")]
    public void A_soft_do_update_carries_the_live_predicate()
    {
        var (sql, parameters) = SoftRootRender.Roots().RenderUpsert(RootValues, [r => r.Id], OnConflict.DoUpdate, RootRename);

        Assert.Equal(
            "INSERT INTO \"soft_roots\" (\"id\", \"name\") VALUES (@p0, @p1) "
            + "ON CONFLICT (\"id\") DO UPDATE SET \"name\" = @p2 WHERE \"soft_roots\".\"deleted_at\" IS NULL",
            sql);
        Assert.Equal(new object?[] { 1, "n", "n" }, parameters);
    }

    [Fact(DisplayName = "AC6: the live predicate of a soft DoUpdate comes before the RETURNING clause")]
    public void The_live_predicate_precedes_returning()
    {
        var (sql, _) = SoftRootRender.Roots().RenderUpsert(RootValues, [r => r.Id], OnConflict.DoUpdate, RootRename, returning: [r => r.Id]);

        Assert.EndsWith(" WHERE \"soft_roots\".\"deleted_at\" IS NULL RETURNING \"id\"", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC6: the escape does not turn a soft DoUpdate into an update of deleted rows")]
    public void The_escape_does_not_lift_the_upsert_guard()
    {
        var (sql, _) = SoftRootRender.Roots().IncludingDeleted().RenderUpsert(RootValues, [r => r.Id], OnConflict.DoUpdate, RootRename);

        Assert.EndsWith(" WHERE \"soft_roots\".\"deleted_at\" IS NULL", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC6 control: DoNothing into a soft entity renders exactly as before, with no live predicate")]
    public void A_soft_do_nothing_is_unchanged()
    {
        var (sql, _) = SoftRootRender.Roots().RenderUpsert(RootValues, [r => r.Id], OnConflict.DoNothing, RootRename);

        Assert.Equal("INSERT INTO \"soft_roots\" (\"id\", \"name\") VALUES (@p0, @p1) ON CONFLICT (\"id\") DO NOTHING", sql);
    }

    [Fact(DisplayName = "AC6 control: a DoUpdate with no assignments is DO NOTHING in the dialect and carries no live predicate")]
    public void A_soft_do_update_without_assignments_is_do_nothing()
    {
        var (sql, _) = SoftRootRender.Roots().RenderUpsert(RootValues, [r => r.Id], OnConflict.DoUpdate);

        Assert.EndsWith(" ON CONFLICT (\"id\") DO NOTHING", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC6 control: a DoUpdate upsert into a non-soft entity renders exactly as before, with no live predicate")]
    public void A_non_soft_do_update_is_unchanged()
    {
        var (sql, _) = NonSoftReferrers().RenderUpsert(ReferrerValues, [r => r.Id], OnConflict.DoUpdate, ReferrerRename);

        Assert.Equal(
            "INSERT INTO \"soft_referrers\" (\"id\", \"name\") VALUES (@p0, @p1) ON CONFLICT (\"id\") DO UPDATE SET \"name\" = @p2",
            sql);
    }

    [Fact(DisplayName = "AC6: the refusal names the table and is an InvalidOperationException")]
    public void The_refusal_names_the_table()
    {
        var refusal = new SoftDeletedConflictException("soft_roots");

        Assert.Equal("soft_roots", refusal.Table);
        Assert.Contains("\"soft_roots\"", refusal.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<InvalidOperationException>(refusal);
    }
}

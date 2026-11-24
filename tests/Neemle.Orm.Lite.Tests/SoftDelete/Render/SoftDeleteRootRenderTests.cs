using System.Linq.Expressions;
using System.Reflection;
using Neemle.Orm.Lite;
using Xunit;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Render;

public sealed class SoftDeleteRootRenderTests
{
    private const string SoftTable = "root_soft_rows";
    private const string PlainTable = "root_plain_rows";
    private const string Live = "\"deleted_at\" IS NULL";

    static SoftDeleteRootRenderTests()
    {
        OrmMeta.Register<SoftRow>(SoftTable, Col, ["id", "name", "rank", "deleted_at"], new SoftDeleteMeta(nameof(SoftRow.DeletedAt)));
        OrmMeta.Register<PlainRow>(PlainTable, Col, ["id", "name", "rank"]);
    }

    [Fact(DisplayName = "a soft root renders the live predicate as the first WHERE term, AND-joined with its Where terms")]
    public void A_soft_root_renders_the_live_predicate_before_its_where_terms()
    {
        Assert.Equal($"SELECT * FROM \"{SoftTable}\" WHERE {Live}", Soft().RenderSelect());
        Assert.Equal(
            $"SELECT * FROM \"{SoftTable}\" WHERE {Live} AND \"rank\" = @p0 AND \"name\" = @p1",
            Soft().Where(r => r.Rank == 1).Where(r => r.Name == "a").RenderSelect());
    }

    [Fact(DisplayName = "every renderable read and write path of a soft root carries the live predicate")]
    public void Every_renderable_path_of_a_soft_root_carries_the_live_predicate()
    {
        var project = new Expression<Func<SoftRow, object?>>[] { r => r.Name };
        string[] rendered =
        [
            Soft().RenderSelect(),
            Soft().RenderProject(project),
            Soft().ProjectColumn(r => r.Name).RenderProjection(),
            Soft().AggregateCount().RenderAggregates(),
            Soft().Where(r => r.Rank == 1).Set(r => r.Name, "x").RenderUpdate(),
            Soft().Where(r => r.Rank == 1).RenderDelete(),
            Soft().Where(r => r.Rank == 1).Set(r => r.Name, "x").RenderUpdateReturning([r => r.Id]),
            Soft().Where(r => r.Rank == 1).RenderDeleteReturning([r => r.Id]),
        ];

        Assert.All(rendered, sql => Assert.Contains(" WHERE " + Live, sql, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "IncludingDeleted clears the live predicate on the root it is called on and returns that root")]
    public void IncludingDeleted_clears_the_live_predicate_on_its_root()
    {
        var root = Soft();
        Assert.Same(root, root.IncludingDeleted());
        Assert.Equal($"SELECT * FROM \"{SoftTable}\"", root.RenderSelect());
        Assert.Equal(
            $"SELECT * FROM \"{SoftTable}\" WHERE \"rank\" = @p0",
            Soft().IncludingDeleted().Where(r => r.Rank == 1).RenderSelect());
    }

    [Fact(DisplayName = "the live predicate never satisfies a refusal: an unfiltered soft root still refuses every write render")]
    public void The_live_predicate_never_satisfies_a_write_refusal()
    {
        Assert.Throws<InvalidOperationException>(() => Soft().Set(r => r.Name, "x").RenderUpdate());
        Assert.Throws<InvalidOperationException>(() => Soft().RenderDelete());
        Assert.Throws<InvalidOperationException>(() => Soft().Set(r => r.Name, "x").RenderUpdateReturning([r => r.Id]));
        Assert.Throws<InvalidOperationException>(() => Soft().RenderDeleteReturning([r => r.Id]));
    }

    [Fact(DisplayName = "the root a WhereIn builder receives starts live, and IncludingDeleted on it clears only that sub-root")]
    public void The_WhereIn_sub_root_starts_live_and_its_escape_is_its_own()
    {
        Assert.Equal(
            $"SELECT * FROM \"{PlainTable}\" WHERE \"id\" IN (SELECT \"id\" FROM \"{SoftTable}\" WHERE {Live} AND \"rank\" = @p0)",
            Plain().WhereIn<SoftRow>(p => p.Id, sub => sub.Where(r => r.Rank == 1), r => r.Id).RenderSelect());

        Assert.Equal(
            $"SELECT * FROM \"{PlainTable}\" WHERE \"id\" IN (SELECT \"id\" FROM \"{SoftTable}\" WHERE \"rank\" = @p0)",
            Plain().WhereIn<SoftRow>(p => p.Id, sub => sub.IncludingDeleted().Where(r => r.Rank == 1), r => r.Id).RenderSelect());

        Assert.Equal(
            $"SELECT * FROM \"{SoftTable}\" WHERE {Live} AND \"id\" IN (SELECT \"id\" FROM \"{SoftTable}\" WHERE \"rank\" = @p0)",
            Soft().WhereIn<SoftRow>(p => p.Id, sub => sub.IncludingDeleted().Where(r => r.Rank == 1), r => r.Id).RenderSelect());
    }

    [Fact(DisplayName = "a root over a type registered without soft-delete metadata renders exactly as before")]
    public void A_plain_root_renders_unchanged()
    {
        Assert.Equal($"SELECT * FROM \"{PlainTable}\"", Plain().RenderSelect());
        Assert.Equal($"DELETE FROM \"{PlainTable}\" WHERE \"rank\" = @p0", Plain().Where(r => r.Rank == 1).RenderDelete());
    }

    [Fact(DisplayName = "re-registering a type through the plain overload drops its soft-delete metadata")]
    public void A_plain_re_registration_drops_soft_delete_metadata()
    {
        OrmMeta.Register<ReRegisteredRow>("root_re_registered", Col, ["id", "deleted_at"], new SoftDeleteMeta(nameof(ReRegisteredRow.DeletedAt)));
        Assert.True(OrmMeta.TryGetSoftDelete(typeof(ReRegisteredRow), out var meta));
        Assert.Equal(nameof(ReRegisteredRow.DeletedAt), meta.Column);

        OrmMeta.Register<ReRegisteredRow>("root_re_registered", Col, ["id", "deleted_at"]);
        Assert.False(OrmMeta.TryGetSoftDelete(typeof(ReRegisteredRow), out _));
    }

    [Fact(DisplayName = "IncludingDeleted is the one escape: a single public parameterless instance method, declared on Query<T> only")]
    public void IncludingDeleted_is_declared_once_on_Query_only()
    {
        var declared = typeof(Query<>).Assembly.GetExportedTypes()
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.Name.Contains("IncludingDeleted", StringComparison.Ordinal))
            .ToList();

        var only = Assert.Single(declared);
        var method = Assert.IsAssignableFrom<MethodInfo>(only);
        Assert.Equal(typeof(Query<>), method.DeclaringType);
        Assert.True(method.IsPublic);
        Assert.False(method.IsStatic);
        Assert.Empty(method.GetParameters());
    }

    private static OrmContext Context() => TestContext.Of(new NeverUsedDataSource(), PostgresProvider.Instance);

    private static Query<SoftRow> Soft() => new(Context(), _ => throw new NotSupportedException());

    private static Query<PlainRow> Plain() => new(Context(), _ => throw new NotSupportedException());

    private static string Col(string property) => property switch
    {
        "Id" => "id",
        "Name" => "name",
        "Rank" => "rank",
        "DeletedAt" => "deleted_at",
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, "not a fixture column"),
    };

    private sealed class SoftRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int Rank { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    private sealed class PlainRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int Rank { get; set; }
    }

    private sealed class ReRegisteredRow
    {
        public Guid Id { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}

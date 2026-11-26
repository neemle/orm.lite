using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphPublicInputTests
{
    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return Case(() => new GraphLimits(null!, new(1, 1, 1)), GraphRequestError.InvalidLimits);
        yield return Case(() => new GraphLimits(new(1, 1, 1), null!), GraphRequestError.InvalidLimits);
        yield return Case(() => new GraphRequest<GraphAuthor>(null!), GraphRequestError.InvalidLimits);
        yield return Case(() => new GraphRequest<GraphAuthor>(GraphDomain.Limits(), null!),
            GraphRequestError.InvalidPath);
        yield return Case(() => new GraphRequest<GraphAuthor>(GraphDomain.Limits(), [null!]),
            GraphRequestError.InvalidPath);
        yield return Case(() => GraphDomain.AuthorBooks.Then<GraphAuthor>(null!), GraphRequestError.InvalidPath);
        yield return Case(() => GraphPaths.Edge<GraphAuthor, GraphBook>(null!), GraphRequestError.InvalidPath);
    }

    private static object[] Case(Action action, GraphRequestError expected) => [action, expected];

    [Theory(DisplayName = "[AC-108-05] null graph inputs fail with the relevant request category")]
    [MemberData(nameof(InvalidRequests))]
    public void Invalid_request_inputs_are_classified(Action create, GraphRequestError expected)
    {
        GraphDomain.Register();
        Assert.Equal(expected, Assert.Throws<GraphRequestException>(create).Error);
    }

    [Fact(DisplayName = "[AC-108-05] invalid entity registrations cannot enter the registry")]
    public void Null_or_duplicate_registration_components_are_refused()
    {
        GraphDomain.Register();
        Func<DbDataReader, GraphAuthor> map = GraphDomain.MapAuthor;
        var key = new GraphKeyBinding<GraphAuthor, int>("id", author => author.Id);
        Action[] invalid =
        [
            () => new GraphEntityBinding<GraphAuthor>(null!, map, key),
            () => new GraphEntityBinding<GraphAuthor>(GraphDomain.Authors, null!, key),
            () => new GraphEntityBinding<GraphAuthor>(GraphDomain.Authors, map, null!),
            () => new GraphEntityBinding<GraphAuthor>(GraphDomain.Authors, map, key, null!),
            () => new GraphEntityBinding<GraphAuthor>(GraphDomain.Authors, map, key, [null!]),
            () => new GraphKeyBinding<GraphAuthor, int>(null!, author => author.Id),
            () => new GraphKeyBinding<GraphAuthor, int>("id", null!),
        ];
        Assert.All(invalid, create =>
            Assert.Equal(GraphRequestError.InvalidBinding, Assert.Throws<GraphRequestException>(create).Error));
        Assert.Throws<ArgumentNullException>(() => GraphModel.Register<GraphAuthor>(null!));
    }

    [Fact(DisplayName = "[AC-108-05] missing registration and impossible bind budgets refuse before opening data")]
    public async Task Preflight_refusals_never_touch_a_data_source()
    {
        GraphDomain.Register();
        var source = new UnreachableDataSource();
        await using var db = new OrmContext(source, new GraphBoundedProvider(0));
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
        Assert.Equal(GraphRequestError.NoBindCapacity, (await Assert.ThrowsAsync<GraphRequestException>(() =>
            new Query<GraphAuthor>(db, GraphDomain.MapAuthor).ToGraphListAsync(request))).Error);
        OrmMeta.Register<SdetUnregisteredRow>(EntityDescriptor.Of("Unregistered", "sdet_unregistered", false)
            .Property("Id", "id", ColumnType.Int32, false, true));
        Assert.Equal(GraphRequestError.UnregisteredEntity, (await Assert.ThrowsAsync<GraphRequestException>(() =>
            new Query<SdetUnregisteredRow>(db, _ => new()).ToGraphListAsync(
                new GraphRequest<SdetUnregisteredRow>(GraphDomain.Limits())))).Error);
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            GraphQueryExtensions.ToGraphListAsync<GraphAuthor>(null!, request));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new Query<GraphAuthor>(db, GraphDomain.MapAuthor).ToGraphListAsync(null!));
        Assert.Equal(0, source.Touches);
    }
}

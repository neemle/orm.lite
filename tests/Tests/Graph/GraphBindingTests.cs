using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphBindingTests
{
    [Fact(DisplayName = "[AC-108-05] registration validates the physical key column and exact scalar key type")]
    public void Invalid_key_metadata_is_refused()
    {
        GraphDomain.Register();
        var wrongColumn = Assert.Throws<GraphRequestException>(() => new GraphEntityBinding<GraphAuthor>(
            GraphDomain.Authors, GraphDomain.MapAuthor, new GraphKeyBinding<GraphAuthor, int>("name", a => a.Id)));
        Assert.Equal(GraphRequestError.InvalidBinding, wrongColumn.Error);
        var wrongType = Assert.Throws<GraphRequestException>(() => new GraphEntityBinding<GraphAuthor>(
            GraphDomain.Authors, GraphDomain.MapAuthor,
            new GraphKeyBinding<GraphAuthor, string>("id", a => a.Id.ToString())));
        Assert.Equal(GraphRequestError.InvalidBinding, wrongType.Error);
    }

    [Fact(DisplayName = "[AC-108-05] equivalent static registration is idempotent and conflicting maps refuse")]
    public void Static_registration_preserves_its_original_binding()
    {
        GraphModel.Register(DoubleBinding());
        GraphModel.Register(DoubleBinding());
        var conflicting = new GraphEntityBinding<GraphTypedKeyRow<double>>(DoubleDescriptor(),
            _ => new() { Id = 42d }, new GraphKeyBinding<GraphTypedKeyRow<double>, double>("id", DoubleKey));
        var error = Assert.Throws<GraphRequestException>(() => GraphModel.Register(conflicting));
        Assert.Equal(GraphRequestError.ConflictingBinding, error.Error);
        Assert.True(GraphModel.Find(typeof(GraphTypedKeyRow<double>)).Maps((Func<DbDataReader,
            GraphTypedKeyRow<double>>)MapDouble));
    }

    [Fact(DisplayName = "[AC-108-05] FK and target key CLR mismatch refuses the whole plan before root I/O")]
    public async Task Incompatible_navigation_bindings_never_open_a_root_reader()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var descriptor = EntityDescriptor.Of("DecimalRow", "graph_incompatible", false)
            .Property("Id", "id", ColumnType.Decimal, false, true)
            .Property("OtherId", "other_id", ColumnType.Int64, false, false);
        Func<DbDataReader, GraphTypedKeyRow<decimal>> map = _ => new() { Id = 1m };
        OrmMeta.Register<GraphTypedKeyRow<decimal>>(descriptor);
        GraphModel.Register(new GraphEntityBinding<GraphTypedKeyRow<decimal>>(descriptor, map,
            new GraphKeyBinding<GraphTypedKeyRow<decimal>, decimal>("id", row => row.Id),
            new GraphReferenceBinding<GraphTypedKeyRow<decimal>, GraphAuthor, long>("Author", "other_id",
                _ => new(true, 1L), (_, _) => { })));
        var request = new GraphRequest<GraphTypedKeyRow<decimal>>(GraphDomain.Limits(),
            GraphPaths.Edge<GraphTypedKeyRow<decimal>, GraphAuthor>("Author"));
        var error = await Assert.ThrowsAsync<GraphRequestException>(() =>
            new Query<GraphTypedKeyRow<decimal>>(db.Context, map).ToGraphListAsync(request));
        Assert.Equal(GraphRequestError.InvalidBinding, error.Error);
        Assert.Empty(db.Statements);
    }

    private static EntityDescriptor DoubleDescriptor() => EntityDescriptor.Of("DoubleRow", "graph_double", false)
        .Property("Id", "id", ColumnType.Double, false, true);
    private static GraphTypedKeyRow<double> MapDouble(DbDataReader reader) => new() { Id = reader.GetDouble(0) };
    private static double DoubleKey(GraphTypedKeyRow<double> row) => row.Id;
    private static GraphEntityBinding<GraphTypedKeyRow<double>> DoubleBinding() => new(DoubleDescriptor(), MapDouble,
        new GraphKeyBinding<GraphTypedKeyRow<double>, double>("id", DoubleKey));
}

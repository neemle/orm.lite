using System.Data.Common;

namespace Neemle.Orm.Lite;

public static class OrmLite
{
    public static async Task<OrmContext> OpenAsync(OrmLiteOptions options, CancellationToken ct = default)
    {
        var factory = ProviderRegistry.Resolve(options);
        var provider = factory.CreateProvider(options);
        var diagnostics = OrmDiagnostics.Create(provider, options.Diagnostics);
        var created = new List<DbDataSource>();
        try
        {
            var request = Lane(factory, options, DbLaneKind.Request, created);
            var background = Lane(factory, options, DbLaneKind.Background, created);
            var ops = Lane(factory, options, DbLaneKind.Ops, created);
            await factory.InitializeAsync(options, background, ct);
            var gated = new GatedDataSource(provider, request, background, ops, diagnostics);
            return new OrmContext(gated, provider, null, created.Cast<IAsyncDisposable>().ToList());
        }
        catch
        {
            foreach (var source in created) await source.DisposeAsync();
            throw;
        }
    }

    private static DbDataSource Lane(IProviderFactory factory, OrmLiteOptions options, DbLaneKind lane, List<DbDataSource> created)
    {
        var source = factory.CreateDataSource(options, lane);
        if (!created.Contains(source)) created.Add(source);
        return source;
    }
}

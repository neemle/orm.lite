using System.Data.Common;

namespace Neemle.OrmLite.Tests.Streaming.Final;

internal static class FinalQueries
{
    internal static Query<StreamRow> Query(StreamProbe probe, ISqlProvider provider)
    {
        _ = probe.Query();
        return Query(new OrmContext(new StreamProbeSource(probe), provider));
    }

    internal static Query<StreamRow> Query(OrmContext context) => new(context,
        r => new StreamRow(r.GetInt32(r.GetOrdinal("id")), r.GetString(r.GetOrdinal("name"))));

    internal static OrmContext Context(StreamProbe probe, OrmLiteDiagnosticsOptions options)
    {
        _ = probe.Query();
        var provider = new StreamProbeProvider(probe);
        var source = new StreamProbeSource(probe);
        var diagnostics = OrmDiagnostics.Create(provider, options);
        return new OrmContext(new GatedDataSource(provider, source, source, source, diagnostics), provider);
    }

    internal static async Task<int> ScalarAsync(DbConnection connection, DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}

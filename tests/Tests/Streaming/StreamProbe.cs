using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.Streaming;

internal sealed class StreamProbe
{
    internal int Count = 3;
    internal int Connections, Opens, Commands, Executions, Reads, Maps, ReaderDisposals, CommandDisposals;
    internal int ConnectionDisposals, Closes;
    internal int ReadFaultAt;
    internal Exception? OpenError, ActorError, ExecuteError, ReadError, ReaderError, CommandError, ConnectionError;
    internal Exception? CloseError;
    internal Func<CancellationToken, Task>? BeforeRead;
    internal bool IgnoreCancellation;
    internal string? Sql;
    internal CancellationToken ReadToken;
    internal DbTransaction? Transaction;
    internal object?[]? BoundValues;
    internal Action<DbCommand>? Executing;

    internal Query<StreamRow> Query(bool gated = false, Func<DbDataReader, StreamRow>? map = null)
    {
        OrmMeta.Register<StreamRow>(EntityDescriptor.Of("StreamRow", "stream_rows", true)
            .Property("Id", "id", ColumnType.Int32, false, true)
            .Property("Name", "name", ColumnType.Text, true, false));
        var source = new StreamProbeSource(this);
        ISqlProvider provider = gated ? new StreamProbeProvider(this) : PostgresProvider.Instance;
        DbDataSource actual = gated ? new GatedDataSource(provider, source, source, source) : source;
        return new Query<StreamRow>(new OrmContext(actual, provider), map ?? Map);
    }

    private StreamRow Map(DbDataReader reader)
    {
        Maps++;
        return new StreamRow(reader.GetInt32(reader.GetOrdinal("id")), reader.GetString(reader.GetOrdinal("name")));
    }
}

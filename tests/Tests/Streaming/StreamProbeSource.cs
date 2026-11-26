using System.Data.Common;

namespace Neemle.OrmLite.Tests.Streaming;

internal sealed class StreamProbeSource(StreamProbe probe) : DbDataSource
{
    public override string ConnectionString => "Host=stream-probe";
    protected override DbConnection CreateDbConnection()
    {
        probe.Connections++;
        return new StreamProbeConnection(probe);
    }
}

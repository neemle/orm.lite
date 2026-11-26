using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.Streaming.Final;

internal sealed class FinalCapabilityProvider(CapabilitySet capabilities) : ISqlProvider
{
    public string Id => "capability-probe";
    public CapabilitySet Capabilities => capabilities;
    public ISerializer CreateSerializer() => PostgresProvider.Instance.CreateSerializer();
}

using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.Corpus;

// The PostgreSQL provider's spelling with every capability withdrawn: the refusal cases of the corpus run against it,
// so each one is turned away by the guard of the query that asked, before any text is written.
public sealed class NoCapabilityProvider : ISqlProvider
{
    public string Id => "no-capability";

    public CapabilitySet Capabilities { get; } = CapabilitySet.Nothing();

    public ISerializer CreateSerializer() => PostgresProvider.Instance.CreateSerializer();
}

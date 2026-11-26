using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sqlite;

namespace Neemle.OrmLite.Tests.Graph;

internal sealed class GraphBoundedProvider(int capacity) : ISqlProvider
{
    public string Id => "sqlite";
    public CapabilitySet Capabilities => SqliteProvider.Instance.Capabilities;
    public int MaxBindParameters => capacity;
    public ISerializer CreateSerializer() => new SqliteSerializer(capacity);
    public Node Lower(Node tree) => SqliteProvider.Instance.Lower(tree);
}

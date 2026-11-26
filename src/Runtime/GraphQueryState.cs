using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class GraphQueryState<T>(OrmContext database, Select root, Func<DbDataReader, T> map)
{
    internal OrmContext Database { get; } = database;
    internal Select Root { get; } = root;
    internal Func<DbDataReader, T> Map { get; } = map;
    internal DbConnection? Connection { get; init; }
    internal DbTransaction? Transaction { get; init; }
    internal IReadOnlyDictionary<string, Node> Masks { get; init; } = new Dictionary<string, Node>();
    internal bool Unsupported { get; init; }
}

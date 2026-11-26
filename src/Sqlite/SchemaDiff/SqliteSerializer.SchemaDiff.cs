using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteSerializer
{
    public SerializedStatement SerializeStrictCreate(CreateTable operation) =>
        new SqliteSerializer(maxBindParameters).StrictCreateCore(operation);
}

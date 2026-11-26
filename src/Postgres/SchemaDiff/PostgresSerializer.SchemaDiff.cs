using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Postgres;

public sealed partial class PostgresSerializer
{
    public SerializedStatement SerializeStrictCreate(CreateTable operation) =>
        new PostgresSerializer().StrictCreateCore(operation);
}

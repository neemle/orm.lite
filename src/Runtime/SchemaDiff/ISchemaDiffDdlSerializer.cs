using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public interface ISchemaDiffDdlSerializer
{
    SerializedStatement SerializeStrictCreate(CreateTable operation);
}

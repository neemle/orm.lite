using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sql;

public abstract partial class StandardSerializer
{
    protected SerializedStatement StrictCreateCore(CreateTable operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        WriteCreateTable(operation, strict: true);
        return new SerializedStatement(W.ToString(), _parameters.ToArray());
    }
}

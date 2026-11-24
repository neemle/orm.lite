using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public interface ISerializer
{
    SerializedStatement Serialize(Node root);

    string Fragment(Node node);
}

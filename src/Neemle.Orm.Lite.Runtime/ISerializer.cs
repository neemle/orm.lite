using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public interface ISerializer
{
    SerializedStatement Serialize(Node root);

    string Fragment(Node node);
}

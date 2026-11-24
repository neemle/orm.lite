using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sql;

namespace Neemle.Orm.Lite.Ports.Tests.Fake;

// The SQL spelling of the fake provider: the standard one, with a colon in front of a parameter name, so that a
// statement it writes cannot be mistaken for the text of any shipped provider. It spells no document, history or lock
// operation; those nodes are refused by the partial of this class that holds the refusals.
public sealed partial class FakeSerializer : StandardSerializer
{
    protected override string ParameterPrefix => ":";

    public override void Visit(TypeRef node)
    {
        switch (node.Kind)
        {
            case ColumnType.Uuid:
                W.Atom("uuid");
                break;
            case ColumnType.Timestamp:
                W.Atom("timestamp");
                break;
            default:
                base.Visit(node);
                break;
        }
    }
}

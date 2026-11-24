using System.Text;

namespace Neemle.OrmLite.Nodes;

public abstract record Node
{
    private static readonly IReadOnlyList<Capability> None = [];

    public string Name => GetType().Name;

    public virtual IReadOnlyList<Capability> Requires() => None;

    public abstract IReadOnlyList<Node> Children();

    public abstract void Accept(INodeVisitor visitor);

    protected virtual bool PrintMembers(StringBuilder builder) => false;

    protected static IReadOnlyList<Node> Slots(params Node?[] slots)
    {
        var present = new List<Node>(slots.Length);
        foreach (var slot in slots)
            if (slot is not null) present.Add(slot);
        return present;
    }
}

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveProbeNode
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "node";
    public List<SaveProbeNode> Children { get; set; } = [];
    public List<SaveProbeTag> Tags { get; set; } = [];
    internal int ChildReads { get; private set; }
    internal int TagReads { get; private set; }
    internal IReadOnlyList<SaveProbeNode> ReadChildren() { ChildReads++; return Children; }
    internal IReadOnlyList<SaveProbeTag> ReadTags() { TagReads++; return Tags; }
}

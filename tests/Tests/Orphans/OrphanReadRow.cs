namespace Neemle.OrmLite.Tests.Orphans;

internal sealed class OrphanReadRow<TMarker>
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    internal int ValuesRead;
    public List<OrphanReadRow<TMarker>> Children { get; set; } = [];
    public OrphanReadRow<TMarker>? Parent { get; set; }
}

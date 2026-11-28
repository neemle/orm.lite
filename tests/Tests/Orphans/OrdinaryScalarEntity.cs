namespace Neemle.OrmLite.Tests.Orphans;

internal sealed class OrdinaryScalarEntity
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Text { get; set; } = "";
    public bool Enabled { get; set; }
    public int Count { get; set; }
    public long Total { get; set; }
    public double Number { get; set; }
    public DateTime? ObservedAt { get; set; }
}

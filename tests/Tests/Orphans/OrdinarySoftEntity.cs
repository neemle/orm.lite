namespace Neemle.OrmLite.Tests.Orphans;

internal sealed class OrdinarySoftEntity
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
}

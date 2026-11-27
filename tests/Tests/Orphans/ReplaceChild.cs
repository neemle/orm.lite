namespace Neemle.OrmLite.Tests.Orphans;

internal sealed class ReplaceChild
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
}

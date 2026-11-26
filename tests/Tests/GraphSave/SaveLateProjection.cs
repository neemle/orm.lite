namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveLateProjection
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
}

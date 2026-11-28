namespace Neemle.OrmLite.Tests.Unit.Joins;

// One entity type per test class (the marker), because OrmMeta and JoinModel are process-wide registries and some
// tests replace a registration while a read is running.
public sealed class UnitJoinRow<TMarker>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Note { get; set; }
}

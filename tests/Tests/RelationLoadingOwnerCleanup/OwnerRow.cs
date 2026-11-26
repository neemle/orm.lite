namespace Neemle.OrmLite.Tests.RelationLoadingOwnerCleanup;

internal sealed class OwnerRow
{
    internal int Id { get; init; }
    internal int NextId { get; init; }
    internal OwnerRow? Next { get; set; }
}

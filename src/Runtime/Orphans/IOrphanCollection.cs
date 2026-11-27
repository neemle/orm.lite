namespace Neemle.OrmLite;

internal interface IOrphanCollection
{
    IOrphanEntity Parent { get; }
    IOrphanEntity Child { get; }
    OrphanCollectionShape Shape { get; }
    ColumnDescriptor Owner { get; }
    void ValidateCurrent();
    bool Equivalent(IOrphanCollection other);
}
